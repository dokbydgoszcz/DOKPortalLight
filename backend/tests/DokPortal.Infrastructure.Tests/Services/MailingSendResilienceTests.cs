using DokPortal.Application.Common;
using DokPortal.Application.Mailing;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Wysyłka kampanii nie gubi i nie dubluje wiadomości: błąd jednego adresu nie przerywa reszty, a wysłana kampania nie idzie drugi raz.</summary>
public class MailingSendResilienceTests
{
    private sealed class ScriptedEmailSender : IEmailSender
    {
        private readonly Func<string, Exception?> _failure;
        public List<string> Delivered { get; } = new();

        public ScriptedEmailSender(Func<string, Exception?> failure) => _failure = failure;

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            var failure = _failure(toEmail);
            if (failure is not null) throw failure;
            Delivered.Add(toEmail);
            return Task.CompletedTask;
        }
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task SeedCandidatesAsync(AppDbContext db, params string[] emails)
    {
        foreach (var email in emails)
        {
            var person = new Person
            {
                Id = Guid.NewGuid(), FirstName = "Jan", LastName = email, Email = email, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            db.People.Add(person);
            db.Candidates.Add(new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationYear = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
    }

    private static Task<MailingCampaignDto> NewCampaignAsync(MailingService service) =>
        service.CreateAsync(new CreateMailingCampaignRequest { Subject = "Zaproszenie", Body = "Treść", Group = MailingGroup.CandidatesSksp }, default);

    [Fact]
    public async Task AFailingAddress_DoesNotStopTheOthers_AndIsCountedSeparately()
    {
        await using var db = CreateContext();
        await SeedCandidatesAsync(db, "a@example.org", "zly@example.org", "c@example.org");
        var sender = new ScriptedEmailSender(to => to == "zly@example.org" ? new InvalidDataException("550 skrzynka nie istnieje") : null);
        var service = new MailingService(db, sender);
        var campaign = await NewCampaignAsync(service);

        var sent = await service.SendAsync(campaign.Id, default);

        Assert.Equal(CampaignStatus.Sent, sent!.Status);
        Assert.Equal(2, sent.RecipientCount);
        Assert.Equal(1, sent.FailedCount);
        Assert.Equal(new[] { "a@example.org", "c@example.org" }, sender.Delivered.OrderBy(x => x));
    }

    [Fact]
    public async Task WhenNothingCanBeSent_TheCampaignStaysADraft_AndTheReasonIsReported()
    {
        await using var db = CreateContext();
        await SeedCandidatesAsync(db, "a@example.org", "b@example.org");
        var service = new MailingService(db, new ScriptedEmailSender(_ => new InvalidDataException("535 złe hasło")));
        var campaign = await NewCampaignAsync(service);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendAsync(campaign.Id, default));

        Assert.Contains("Nie udało się wysłać żadnej wiadomości", ex.Message);
        Assert.Contains("535 złe hasło", ex.Message);
        var stored = await db.MailingCampaigns.AsNoTracking().SingleAsync();
        Assert.Equal((CampaignStatus.Draft, 0), (stored.Status, stored.FailedCount));
    }

    [Fact]
    public async Task AMissingSmtpConfiguration_KeepsItsOwnClearMessage_AndLeavesTheDraft()
    {
        await using var db = CreateContext();
        await SeedCandidatesAsync(db, "a@example.org");
        var service = new MailingService(db, new NullEmailSender());
        var campaign = await NewCampaignAsync(service);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendAsync(campaign.Id, default));

        Assert.Contains("nie jest skonfigurowane", ex.Message);
        Assert.Equal(CampaignStatus.Draft, (await db.MailingCampaigns.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task ASentCampaign_CannotBeSentAgain_SoNobodyGetsItTwice()
    {
        await using var db = CreateContext();
        await SeedCandidatesAsync(db, "a@example.org");
        var sender = new ScriptedEmailSender(_ => null);
        var service = new MailingService(db, sender);
        var campaign = await NewCampaignAsync(service);
        await service.SendAsync(campaign.Id, default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendAsync(campaign.Id, default));

        Assert.Contains("już wysłana", ex.Message);
        Assert.Single(sender.Delivered);
    }

    [Fact]
    public async Task AnAddressThatDiffersOnlyInCase_IsMailedOnce()
    {
        await using var db = CreateContext();
        await SeedCandidatesAsync(db, "Jan@Example.org", "jan@example.org");
        var sender = new ScriptedEmailSender(_ => null);
        var service = new MailingService(db, sender);
        var campaign = await NewCampaignAsync(service);

        var sent = await service.SendAsync(campaign.Id, default);

        Assert.Equal(1, sent!.RecipientCount);
        Assert.Single(sender.Delivered);
    }
}
