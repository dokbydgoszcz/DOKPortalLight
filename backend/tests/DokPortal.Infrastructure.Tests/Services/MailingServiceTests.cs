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
        db.Candidates.Add(new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, Year = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
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
            new Candidate { Id = Guid.NewGuid(), PersonId = withEmail.Id, Year = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Candidate { Id = Guid.NewGuid(), PersonId = withoutEmail.Id, Year = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
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

    private class RecordingEmailSender : IEmailSender
    {
        public List<string> SentTo { get; } = new();

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            SentTo.Add(toEmail);
            return Task.CompletedTask;
        }
    }
}
