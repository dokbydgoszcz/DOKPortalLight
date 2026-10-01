using DokPortal.Application.Common;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class ReminderServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static Person NewPerson(string? email = null) => new()
    {
        Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", Email = email,
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static DokCase NewDokCase(Guid personId, Guid catechistPersonId) => new()
    {
        Id = Guid.NewGuid(), PersonId = personId, CatechistPersonId = catechistPersonId,
        Path = DokPath.Confirmation, Stage = DokStage.Formation,
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static CaseDocument NewMissingDocument(Guid dokCaseId, string name, DateTime? lastReminderSentAtUtc = null) => new()
    {
        Id = Guid.NewGuid(), DokCaseId = dokCaseId, Name = name, IsProvided = false,
        CreatedAtUtc = DateTime.UtcNow, LastReminderSentAtUtc = lastReminderSentAtUtc
    };

    private class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            Sent.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private class ThrowingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct) =>
            throw new InvalidOperationException("Wysyłanie e-maili nie jest skonfigurowane.");
    }

    [Fact]
    public async Task RunAsync_SkipsDocumentRemindedLessThanSevenDaysAgo()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        db.CaseDocuments.Add(NewMissingDocument(dokCase.Id, "Metryka chrztu", DateTime.UtcNow.AddDays(-3)));
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(0, result.CasesProcessed);
        Assert.Equal(0, result.EmailsSentToCatechists);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task RunAsync_IncludesDocumentNeverRemindedOrOlderThanSevenDays()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        db.CaseDocuments.AddRange(
            NewMissingDocument(dokCase.Id, "Metryka chrztu", null),
            NewMissingDocument(dokCase.Id, "Zaświadczenie", DateTime.UtcNow.AddDays(-8)));
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(1, result.CasesProcessed);
        Assert.Equal(1, result.EmailsSentToCatechists);
        Assert.Single(emailSender.Sent);
        Assert.Equal("katechista@example.org", emailSender.Sent[0].To);
        Assert.Contains("Metryka chrztu", emailSender.Sent[0].Body);
        Assert.Contains("Zaświadczenie", emailSender.Sent[0].Body);
    }

    [Fact]
    public async Task RunAsync_GroupsMultipleMissingDocumentsInSameCaseIntoOneEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        db.CaseDocuments.AddRange(
            NewMissingDocument(dokCase.Id, "Dokument A"),
            NewMissingDocument(dokCase.Id, "Dokument B"),
            NewMissingDocument(dokCase.Id, "Dokument C"));
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(1, result.CasesProcessed);
        Assert.Equal(1, result.EmailsSentToCatechists);
        Assert.Single(emailSender.Sent);
    }

    [Fact]
    public async Task RunAsync_StampsLastReminderSentAtUtcOnlyAfterSuccessfulCatechistEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        var document = NewMissingDocument(dokCase.Id, "Metryka chrztu");
        db.CaseDocuments.Add(document);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);
        await service.RunMissingDocumentsReminderAsync(default);

        var reloaded = await db.CaseDocuments.AsNoTracking().SingleAsync(d => d.Id == document.Id);
        Assert.NotNull(reloaded.LastReminderSentAtUtc);
        Assert.True(reloaded.LastReminderSentAtUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task RunAsync_DoesNotStampWhenCatechistHasNoEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson(email: null);
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        var document = NewMissingDocument(dokCase.Id, "Metryka chrztu");
        db.CaseDocuments.Add(document);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);
        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(0, result.EmailsSentToCatechists);
        var reloaded = await db.CaseDocuments.AsNoTracking().SingleAsync(d => d.Id == document.Id);
        Assert.Null(reloaded.LastReminderSentAtUtc);
    }

    [Fact]
    public async Task RunAsync_DoesNotStampWhenCatechistEmailSendFails()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        var document = NewMissingDocument(dokCase.Id, "Metryka chrztu");
        db.CaseDocuments.Add(document);
        await db.SaveChangesAsync();

        var service = new ReminderService(db, new ThrowingEmailSender(), NullLogger<ReminderService>.Instance);
        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(0, result.EmailsSentToCatechists);
        Assert.Equal(1, result.FailedSends);
        var reloaded = await db.CaseDocuments.AsNoTracking().SingleAsync(d => d.Id == document.Id);
        Assert.Null(reloaded.LastReminderSentAtUtc);
    }

    [Fact]
    public async Task RunAsync_SendsDigestToDyrektorDokUsersWithEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        db.CaseDocuments.Add(NewMissingDocument(dokCase.Id, "Metryka chrztu"));

        var role = new IdentityRole(AppRoles.DyrektorDOK) { NormalizedName = AppRoles.DyrektorDOK.ToUpperInvariant() };
        db.Roles.Add(role);
        var director = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "dyrektor@example.org", Email = "dyrektor@example.org" };
        db.Users.Add(director);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = director.Id, RoleId = role.Id });
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);
        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.True(result.DirectorsSummarySent);
        Assert.Contains(emailSender.Sent, s => s.To == "dyrektor@example.org");
    }

    [Fact]
    public async Task RunAsync_WithNoMissingDocuments_ReturnsZeroedResultAndSendsNoDigest()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(0, result.CasesProcessed);
        Assert.False(result.DirectorsSummarySent);
        Assert.Empty(emailSender.Sent);
    }
}
