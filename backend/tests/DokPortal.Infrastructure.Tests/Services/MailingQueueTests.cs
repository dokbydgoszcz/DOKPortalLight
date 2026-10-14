using DokPortal.Application.Common;
using DokPortal.Application.Mailing;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Kolejka kampanii: dobowy limit skrzynki (Gmail), wznawianie, wypisywanie się z mailingu.</summary>
public class MailingQueueTests
{
    private sealed class Recorder : IEmailSender
    {
        public Func<string, Exception?> Failure { get; set; } = _ => null;
        public List<(string To, string Body)> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            if (Failure(toEmail) is { } ex) throw ex;
            Sent.Add((toEmail, body));
            return Task.CompletedTask;
        }
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<List<Person>> SeedCandidatesAsync(AppDbContext db, int count)
    {
        var people = new List<Person>();
        for (var i = 0; i < count; i++)
        {
            var person = new Person
            {
                Id = Guid.NewGuid(), FirstName = "Jan", LastName = $"Nr{i:00}", Email = $"jan{i:00}@example.org", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            people.Add(person);
            db.People.Add(person);
            db.Candidates.Add(new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationYear = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
        return people;
    }

    private static Task<MailingCampaignDto> NewCampaignAsync(MailingService service) =>
        service.CreateAsync(new CreateMailingCampaignRequest { Subject = "Zaproszenie", Body = "Treść", Group = MailingGroup.CandidatesSksp }, default);

    [Fact]
    public async Task ABigCampaign_GoesOutInDailyPortions_AndFinishesTheNextDay()
    {
        await using var db = CreateContext();
        await SeedCandidatesAsync(db, 7);
        var time = new MovableClock(new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero));
        var mail = new Recorder();
        var service = new MailingService(db, mail, time, options: new MailingOptions { DailyLimit = 3 });
        var campaign = await NewCampaignAsync(service);

        var queued = await service.SendAsync(campaign.Id, default);
        Assert.Equal((CampaignStatus.Sending, 1, 6), (queued!.Status, queued.RecipientCount, queued.PendingCount));

        var first = await service.ProcessQueueAsync(default);
        Assert.Equal((2, 4, 3, 3), (first.Sent, first.Pending, first.SentLast24h, first.DailyLimit));
        Assert.Equal(0, (await service.ProcessQueueAsync(default)).Sent);
        var listed = (await service.ListAsync(default)).Single();
        Assert.Equal((CampaignStatus.Sending, 3, 4), (listed.Status, listed.RecipientCount, listed.PendingCount));

        time.Advance(TimeSpan.FromHours(24));
        await service.ProcessQueueAsync(default);
        time.Advance(TimeSpan.FromHours(24));
        var last = await service.ProcessQueueAsync(default);

        Assert.Equal(0, last.Pending);
        var done = (await service.ListAsync(default)).Single();
        Assert.Equal((CampaignStatus.Sent, 7, 0), (done.Status, done.RecipientCount, done.PendingCount));
        Assert.Equal(7, mail.Sent.Select(m => m.To).Distinct().Count());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendAsync(campaign.Id, default));
    }

    [Fact]
    public async Task WhenTheLimitIsAlreadyUsedUp_TheCampaignJustWaitsInTheQueue()
    {
        await using var db = CreateContext();
        await SeedCandidatesAsync(db, 2);
        var mail = new Recorder();
        var service = new MailingService(db, mail, options: new MailingOptions { DailyLimit = 0 });
        var campaign = await NewCampaignAsync(service);

        var queued = await service.SendAsync(campaign.Id, default);

        Assert.Equal((CampaignStatus.Sending, 2), (queued!.Status, queued.PendingCount));
        Assert.Empty(mail.Sent);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendAsync(campaign.Id, default));
    }

    [Fact]
    public async Task EachMessageHasAnUnsubscribeLink_AndThePersonCanLeaveAndComeBack()
    {
        await using var db = CreateContext();
        var people = await SeedCandidatesAsync(db, 2);
        var mail = new Recorder();
        var service = new MailingService(db, mail, options: new MailingOptions(), portalUrl: new PortalUrl("https://portal.example.org/"));

        await service.SendAsync((await NewCampaignAsync(service)).Id, default);
        await service.ProcessQueueAsync(default);

        Assert.All(mail.Sent, m => Assert.StartsWith("Treść\n\n—\n", m.Body));
        var link = mail.Sent[0].Body.Split('\n').Last();
        Assert.StartsWith("Nie chcesz dostawać takich wiadomości? Wypisz się: https://portal.example.org/wypisz/", link);
        var token = link[(link.LastIndexOf('/') + 1)..];
        Assert.NotEqual(token, mail.Sent[1].Body.Split('/').Last());

        Assert.Equal(("Jan", false), Pair(await service.GetSubscriptionAsync(token, default)));
        Assert.Equal(("Jan", true), Pair(await service.SetOptOutAsync(token, true, default)));
        Assert.Null(await service.GetSubscriptionAsync("nieznany", default));
        Assert.Null(await service.SetOptOutAsync("", true, default));

        var recipients = await service.GetRecipientsAsync(MailingGroup.CandidatesSksp, null, default);
        Assert.Single(recipients, r => r.OptedOut);

        mail.Sent.Clear();
        await service.SendAsync((await NewCampaignAsync(service)).Id, default);
        await service.ProcessQueueAsync(default);
        Assert.Single(mail.Sent);

        await service.SetOptOutAsync(token, false, default);
        Assert.Equal(("Jan", false), Pair(await service.GetSubscriptionAsync(token, default)));
        // Ten sam klucz przy kolejnej kampanii – link z poprzedniej wiadomości nadal działa.
        mail.Sent.Clear();
        await service.SendAsync((await NewCampaignAsync(service)).Id, default);
        await service.ProcessQueueAsync(default);
        Assert.Contains(mail.Sent, m => m.Body.EndsWith(token));
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static (string, bool) Pair(MailingSubscriptionDto? dto) => (dto!.FirstName, dto.OptedOut);

    [Fact]
    public async Task SomeoneWhoUnsubscribesWhileTheCampaignWaits_IsSkipped()
    {
        await using var db = CreateContext();
        var people = await SeedCandidatesAsync(db, 3);
        var mail = new Recorder();
        var service = new MailingService(db, mail);
        await service.SendAsync((await NewCampaignAsync(service)).Id, default);

        people[2].MailingOptOutAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await service.ProcessQueueAsync(default);

        Assert.Equal(2, mail.Sent.Count);
        var done = (await service.ListAsync(default)).Single();
        Assert.Equal((CampaignStatus.Sent, 2, 0), (done.Status, done.RecipientCount, done.FailedCount));
    }

    [Fact]
    public async Task AMissingMailConfigurationInTheBackground_LeavesTheQueueForLater()
    {
        await using var db = CreateContext();
        await SeedCandidatesAsync(db, 3);
        var mail = new Recorder();
        var service = new MailingService(db, mail);
        await service.SendAsync((await NewCampaignAsync(service)).Id, default);

        mail.Failure = _ => new InvalidOperationException("nie jest skonfigurowane");
        var stuck = await service.ProcessQueueAsync(default);
        Assert.Equal((0, 2), (stuck.Sent, stuck.Pending));

        mail.Failure = _ => null;
        Assert.Equal(2, (await service.ProcessQueueAsync(default)).Sent);
    }

    [Fact]
    public async Task ManyFailuresInARow_StopTheRun_AndTheAddressesAreRetriedLater()
    {
        await using var db = CreateContext();
        await SeedCandidatesAsync(db, 6);
        var mail = new Recorder();
        var service = new MailingService(db, mail);
        await service.SendAsync((await NewCampaignAsync(service)).Id, default);

        mail.Failure = _ => new IOException("421 spróbuj później");
        var blocked = await service.ProcessQueueAsync(default);
        Assert.Equal((0, 3, 5), (blocked.Sent, blocked.Failed, blocked.Pending));

        mail.Failure = _ => null;
        var resumed = await service.ProcessQueueAsync(default);
        Assert.Equal((5, 0), (resumed.Sent, resumed.Pending));
        Assert.Equal(0, (await service.ListAsync(default)).Single().FailedCount);
    }

    [Fact]
    public async Task ACampaignWhoseEveryoneUnsubscribed_IsClosedByTheNextRun()
    {
        await using var db = CreateContext();
        var people = await SeedCandidatesAsync(db, 1);
        var service = new MailingService(db, new Recorder(), options: new MailingOptions { DailyLimit = 0 });
        await service.SendAsync((await NewCampaignAsync(service)).Id, default);
        people[0].MailingOptOutAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        db.MailingDeliveries.RemoveRange(db.MailingDeliveries);
        await db.SaveChangesAsync();

        await service.ProcessQueueAsync(default);

        Assert.Equal(CampaignStatus.Sent, (await service.ListAsync(default)).Single().Status);
    }
}
