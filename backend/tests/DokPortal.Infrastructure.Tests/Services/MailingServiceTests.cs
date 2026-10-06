using DokPortal.Domain.Formation;
using DokPortal.Application.Common;
using DokPortal.Application.Mailing;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class MailingServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static Person NewPerson() => new()
    {
        Id = Guid.NewGuid(), FirstName = "Test", LastName = "Osoba",
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task GetRecipientCountAsync_CountsDokGraduatesAndDokCasesSeparately()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson();
        db.People.AddRange(person, catechist);
        db.DokCases.AddRange(
            new DokCase { Id = Guid.NewGuid(), PersonId = person.Id, Path = DokPath.Confirmation, Stage = DokStage.Graduate, CatechistPersonId = catechist.Id, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new DokCase { Id = Guid.NewGuid(), PersonId = person.Id, Path = DokPath.Confirmation, Stage = DokStage.Formation, CatechistPersonId = catechist.Id, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new MailingService(db, new NullEmailSender());

        Assert.Equal(1, await service.GetRecipientCountAsync(MailingGroup.DokGraduates, default));
        Assert.Equal(2, await service.GetRecipientCountAsync(MailingGroup.DokCases, default));
    }

    [Fact]
    public async Task CreateAsync_SnapshotsRecipientCountAtCreationTime()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        db.People.Add(person);
        db.Candidates.Add(new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = FormationCalendar.StartYearFor(1, DateOnly.FromDateTime(DateTime.UtcNow)), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new MailingService(db, new NullEmailSender());
        var created = await service.CreateAsync(
            new CreateMailingCampaignRequest { Subject = "Zaproszenie", Body = "Treść", Group = MailingGroup.CandidatesSksp }, default);

        Assert.Equal(1, created.RecipientCount);
        Assert.Equal(CampaignStatus.Draft, created.Status);
    }

    [Fact]
    public async Task SendAsync_FlipsStatusAndStampsSentAtUtc()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new MailingService(db, new NullEmailSender());
        var created = await service.CreateAsync(
            new CreateMailingCampaignRequest { Subject = "Zaproszenie", Body = "Treść", Group = MailingGroup.DokCases }, default);

        var sent = await service.SendAsync(created.Id, default);

        Assert.NotNull(sent);
        Assert.Equal(CampaignStatus.Sent, sent!.Status);
        Assert.NotNull(sent.SentAtUtc);
    }

    [Fact]
    public async Task SendAsync_SendsEmailToEachRecipientWithAnAddress()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var withEmail = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", Email = "jan.kowalski@example.org",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        var withoutEmail = NewPerson();
        db.People.AddRange(withEmail, withoutEmail);
        db.Candidates.AddRange(
            new Candidate { Id = Guid.NewGuid(), PersonId = withEmail.Id, FormationStartYear = FormationCalendar.StartYearFor(1, DateOnly.FromDateTime(DateTime.UtcNow)), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Candidate { Id = Guid.NewGuid(), PersonId = withoutEmail.Id, FormationStartYear = FormationCalendar.StartYearFor(1, DateOnly.FromDateTime(DateTime.UtcNow)), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new MailingService(db, emailSender);
        var created = await service.CreateAsync(
            new CreateMailingCampaignRequest { Subject = "Zaproszenie", Body = "Treść", Group = MailingGroup.CandidatesSksp }, default);

        var sent = await service.SendAsync(created.Id, default);

        Assert.NotNull(sent);
        Assert.Equal(1, sent!.RecipientCount);
        Assert.Single(emailSender.SentTo);
        Assert.Equal("jan.kowalski@example.org", emailSender.SentTo[0]);
    }

    [Fact]
    public async Task RecipientCounts_CandidatesAreOnlyThoseInFormation_AndCatechistsIncludeThoseAwaitingTheMission()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var inFormation = NewPerson();
        var stopped = NewPerson();
        var finished = NewPerson();
        var finishedAndSent = NewPerson();
        var oldMissionary = NewPerson();
        db.People.AddRange(inFormation, stopped, finished, finishedAndSent, oldMissionary);
        db.Candidates.AddRange(
            new Candidate { Id = Guid.NewGuid(), PersonId = inFormation.Id, FormationStartYear = 2026, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Candidate { Id = Guid.NewGuid(), PersonId = stopped.Id, FormationStartYear = 2026, IsFormationStopped = true, FormationStopNote = "x", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Candidate { Id = Guid.NewGuid(), PersonId = finished.Id, FormationStartYear = 2022, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Candidate { Id = Guid.NewGuid(), PersonId = finishedAndSent.Id, FormationStartYear = 2022, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        db.CanonicalMissions.AddRange(
            new CanonicalMission { Id = Guid.NewGuid(), PersonId = finishedAndSent.Id, ServicePlace = "x", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new CanonicalMission { Id = Guid.NewGuid(), PersonId = oldMissionary.Id, ServicePlace = "y", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = new MailingService(db, new NullEmailSender(), new FixedTimeProvider(2026, 10, 3));

        Assert.Equal(1, await service.GetRecipientCountAsync(MailingGroup.CandidatesSksp, default));
        Assert.Equal(3, await service.GetRecipientCountAsync(MailingGroup.Missionaries, default));
    }

    private class FailingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct) =>
            throw new System.Net.Mail.SmtpException("5.7.57 Client not authenticated to send mail");
    }

    [Fact]
    public async Task SendTestAsync_SendsOneMessageToTheGivenAddress_ExplainingWhatItIs()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var sender = new RecordingEmailSender();

        await new MailingService(db, sender).SendTestAsync("admin@example.org", default);

        Assert.Equal(new[] { "admin@example.org" }, sender.SentTo);
        Assert.Equal("Wiadomość testowa z DOK Portal", sender.LastSubject);
        Assert.Contains("działa", sender.LastBody);
    }

    [Fact]
    public async Task SendTestAsync_WhenTheServerRefuses_ExplainsTheReasonInPolish_WithTheServerMessage()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new MailingService(db, new FailingEmailSender()).SendTestAsync("admin@example.org", default));

        Assert.Contains("Nie udało się wysłać wiadomości", ex.Message);
        Assert.Contains("5.7.57 Client not authenticated", ex.Message);
    }

    [Fact]
    public async Task SendTestAsync_WithoutSmtp_KeepsTheNotConfiguredMessage()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new MailingService(db, new NullEmailSender()).SendTestAsync("admin@example.org", default));

        Assert.Contains("nie jest skonfigurowane", ex.Message);
        Assert.DoesNotContain("Nie udało się wysłać", ex.Message);
    }

    private class RecordingEmailSender : IEmailSender
    {
        public List<string> SentTo { get; } = new();
        public string? LastSubject { get; private set; }
        public string? LastBody { get; private set; }

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            SentTo.Add(toEmail);
            LastSubject = subject;
            LastBody = body;
            return Task.CompletedTask;
        }
    }
}
