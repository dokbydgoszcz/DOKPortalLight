using DokPortal.Api.Mailing;
using DokPortal.Application.Mailing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Wątek w tle wysyłający kolejkę mailingu: jeden przebieg naraz, ponowienie, gdy coś zostało, odporność na błędy.</summary>
public class MailingQueueRunnerTests
{
    private sealed class FakeMailing : IMailingService
    {
        public int Runs;
        public Func<int, Task<MailingQueueResultDto>> Next { get; set; } = _ => Task.FromResult(Result(0));

        public static MailingQueueResultDto Result(int pending) => new() { Sent = 1, Failed = 0, Pending = pending, DailyLimit = 400, SentLast24h = 1 };

        public Task<MailingQueueResultDto> ProcessQueueAsync(CancellationToken ct) => Next(Interlocked.Increment(ref Runs));
        public Task<IReadOnlyList<MailingCampaignDto>> ListAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MailingCampaignDto> CreateAsync(CreateMailingCampaignRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<MailingCampaignDto?> UpdateDraftAsync(Guid id, CreateMailingCampaignRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<MailingCampaignDto?> SendAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> DeleteDraftAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool?> IsDraftAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task SendTestAsync(string toEmail, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> GetRecipientCountAsync(DokPortal.Domain.Enums.MailingGroup group, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<MailingRecipientDto>> GetRecipientsAsync(DokPortal.Domain.Enums.MailingGroup group, IReadOnlyList<Guid>? personIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<MailingRecipientDto>?> GetCampaignRecipientsAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<MailingSubscriptionDto?> GetSubscriptionAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task<MailingSubscriptionDto?> SetOptOutAsync(string token, bool optOut, CancellationToken ct) => throw new NotSupportedException();
    }

    private static (MailingQueueRunner Runner, FakeMailing Mailing) Create(Dictionary<string, string?>? settings = null, TimeSpan? retry = null)
    {
        var mailing = new FakeMailing();
        var services = new ServiceCollection().AddSingleton<IMailingService>(mailing).BuildServiceProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new()).Build();
        var runner = new MailingQueueRunner(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<MailingQueueRunner>.Instance, config)
        {
            RetryAfter = retry ?? TimeSpan.FromHours(1)
        };
        return (runner, mailing);
    }

    [Fact]
    public async Task AKickDuringARun_TriggersOneMoreRun_NotAParallelOne()
    {
        var (runner, mailing) = Create();
        var gate = new TaskCompletionSource();
        mailing.Next = async run =>
        {
            if (run == 1) await gate.Task;
            return FakeMailing.Result(0);
        };

        runner.Kick();
        var first = runner.Current;
        runner.Kick();
        runner.Kick();
        gate.SetResult();
        await first;

        Assert.Equal(2, mailing.Runs);
        runner.Dispose();
    }

    [Fact]
    public async Task WhenMessagesRemain_ItTriesAgainLater_AndSurvivesErrors()
    {
        var (runner, mailing) = Create(retry: TimeSpan.FromMilliseconds(50));
        mailing.Next = run => run switch
        {
            1 => Task.FromResult(FakeMailing.Result(5)),
            2 => throw new InvalidOperationException("baza śpi"),
            _ => Task.FromResult(FakeMailing.Result(0))
        };

        runner.Kick();
        await runner.Current;
        for (var i = 0; i < 100 && mailing.Runs < 2; i++) await Task.Delay(20);
        Assert.Equal(2, mailing.Runs);

        runner.Kick();
        await runner.Current;
        Assert.Equal(3, mailing.Runs);
        await runner.StopAsync(default);
        runner.Dispose();
    }

    [Fact]
    public async Task OnStartup_ItResumesUnlessTurnedOff_AndStoppingCancelsARun()
    {
        var (off, offMailing) = Create(new() { ["Mailing:ResumeOnStartup"] = "false" });
        await off.StartAsync(default);
        await off.StopAsync(default);
        Assert.Equal(0, offMailing.Runs);
        off.Dispose();

        var (runner, mailing) = Create();
        await runner.StartAsync(default);
        mailing.Next = async _ =>
        {
            await Task.Delay(50);
            throw new OperationCanceledException();
        };
        runner.Kick();
        await runner.StopAsync(default);
        await runner.Current;
        Assert.Equal(1, mailing.Runs);

        // Po zatrzymaniu nic już nie rusza, a zatrzymanie i zwolnienie można powtarzać w dowolnej kolejności.
        runner.Kick();
        await runner.Current;
        Assert.Equal(1, mailing.Runs);
        runner.Dispose();
        await runner.StopAsync(default);
        runner.Dispose();
    }
}
