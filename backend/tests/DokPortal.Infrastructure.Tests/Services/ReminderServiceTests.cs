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
        Path = DokPath.Confirmation, Stage = DokStage.Evangelization,
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
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

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
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

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
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

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
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));
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
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));
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

        var service = new ReminderService(db, new ThrowingEmailSender(), NullLogger<ReminderService>.Instance, new NameDayService(db));
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
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));
        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.True(result.DirectorsSummarySent);
        Assert.Contains(emailSender.Sent, s => s.To == "dyrektor@example.org");
    }

    [Fact]
    public async Task RunAsync_WithNoMissingDocuments_ReturnsZeroedResultAndSendsNoDigest()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(0, result.CasesProcessed);
        Assert.False(result.DirectorsSummarySent);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task RunUpcomingMeetingsReminderAsync_SendsReminderForMeetingTomorrowWithCase()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        var meeting = new Meeting
        {
            Id = Guid.NewGuid(), DokCaseId = dokCase.Id,
            MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(1, result.MeetingsProcessed);
        Assert.Equal(1, result.EmailsSentToCatechists);
        Assert.Single(emailSender.Sent);
        Assert.Equal("katechista@example.org", emailSender.Sent[0].To);

        var reloaded = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == meeting.Id);
        Assert.NotNull(reloaded.ReminderSentAtUtc);
    }

    [Fact]
    public async Task RunUpcomingMeetingsReminderAsync_SkipsMeetingNotHappeningTomorrow()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        db.Meetings.AddRange(
            new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2), CreatedAtUtc = DateTime.UtcNow },
            new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(0, result.MeetingsProcessed);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task RunUpcomingMeetingsReminderAsync_SkipsGroupMeetingWithoutDokCase()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        db.Meetings.Add(new Meeting
        {
            Id = Guid.NewGuid(), DokCaseId = null, GroupLabel = "Spotkanie grupowe",
            MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(0, result.MeetingsProcessed);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task RunUpcomingMeetingsReminderAsync_SkipsWhenCatechistHasNoEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson(email: null);
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        var meeting = new Meeting
        {
            Id = Guid.NewGuid(), DokCaseId = dokCase.Id,
            MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), CreatedAtUtc = DateTime.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(1, result.MeetingsProcessed);
        Assert.Equal(0, result.EmailsSentToCatechists);
        var reloaded = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == meeting.Id);
        Assert.Null(reloaded.ReminderSentAtUtc);
    }

    [Fact]
    public async Task RunUpcomingMeetingsReminderAsync_DoesNotStampWhenSendFails()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        var meeting = new Meeting
        {
            Id = Guid.NewGuid(), DokCaseId = dokCase.Id,
            MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), CreatedAtUtc = DateTime.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var service = new ReminderService(db, new ThrowingEmailSender(), NullLogger<ReminderService>.Instance, new NameDayService(db));
        var result = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(0, result.EmailsSentToCatechists);
        Assert.Equal(1, result.FailedSends);
        var reloaded = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == meeting.Id);
        Assert.Null(reloaded.ReminderSentAtUtc);
    }

    [Fact]
    public async Task RunUpcomingMeetingsReminderAsync_SkipsMeetingAlreadyReminded()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        db.Meetings.Add(new Meeting
        {
            Id = Guid.NewGuid(), DokCaseId = dokCase.Id,
            MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), CreatedAtUtc = DateTime.UtcNow,
            ReminderSentAtUtc = DateTime.UtcNow.AddHours(-2)
        });
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(0, result.MeetingsProcessed);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task RunUpcomingNameDaysReminderAsync_WithNoUpcomingNameDays_SendsNothing()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var user = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "user@example.org", Email = "user@example.org" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingNameDaysReminderAsync(default);

        Assert.Equal(0, result.NameDaysFound);
        Assert.Equal(0, result.RecipientsNotified);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task RunUpcomingNameDaysReminderAsync_SendsDigestToAllUsersWithEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var today = DateTime.UtcNow;
        var soon = today.AddDays(2);
        db.People.Add(new Person
        {
            Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski",
            NameDayMonth = soon.Month, NameDayDay = soon.Day,
            CreatedAtUtc = today, UpdatedAtUtc = today
        });
        var userA = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "a@example.org", Email = "a@example.org" };
        var userB = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "b@example.org", Email = "b@example.org" };
        db.Users.AddRange(userA, userB);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingNameDaysReminderAsync(default);

        Assert.Equal(1, result.NameDaysFound);
        Assert.Equal(2, result.RecipientsNotified);
        Assert.Equal(2, emailSender.Sent.Count);
        Assert.Contains(emailSender.Sent, s => s.To == "a@example.org" && s.Body.Contains("Jan Kowalski"));
        Assert.Contains(emailSender.Sent, s => s.To == "b@example.org" && s.Body.Contains("Jan Kowalski"));
    }

    [Fact]
    public async Task RunUpcomingNameDaysReminderAsync_SkipsUsersWithoutEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var today = DateTime.UtcNow;
        var soon = today.AddDays(1);
        db.People.Add(new Person
        {
            Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Nowak",
            NameDayMonth = soon.Month, NameDayDay = soon.Day,
            CreatedAtUtc = today, UpdatedAtUtc = today
        });
        var userWithEmail = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "with@example.org", Email = "with@example.org" };
        var userWithoutEmail = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "without" };
        db.Users.AddRange(userWithEmail, userWithoutEmail);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingNameDaysReminderAsync(default);

        Assert.Equal(1, result.RecipientsNotified);
        Assert.Single(emailSender.Sent);
        Assert.Equal("with@example.org", emailSender.Sent[0].To);
    }

    [Fact]
    public async Task RunUpcomingNameDaysReminderAsync_ContinuesAfterOneRecipientFails()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var today = DateTime.UtcNow;
        var soon = today.AddDays(1);
        db.People.Add(new Person
        {
            Id = Guid.NewGuid(), FirstName = "Piotr", LastName = "Zalewski",
            NameDayMonth = soon.Month, NameDayDay = soon.Day,
            CreatedAtUtc = today, UpdatedAtUtc = today
        });
        var okUser = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "ok@example.org", Email = "ok@example.org" };
        var failingUser = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "failing@example.org", Email = "failing@example.org" };
        db.Users.AddRange(okUser, failingUser);
        await db.SaveChangesAsync();

        var emailSender = new SelectivelyFailingEmailSender("failing@example.org");
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingNameDaysReminderAsync(default);

        Assert.Equal(1, result.RecipientsNotified);
        Assert.Equal(1, result.FailedSends);
        Assert.Single(emailSender.Sent);
        Assert.Equal("ok@example.org", emailSender.Sent[0]);
    }

    private class SelectivelyFailingEmailSender : IEmailSender
    {
        private readonly string _failingEmail;
        public List<string> Sent { get; } = new();

        public SelectivelyFailingEmailSender(string failingEmail) => _failingEmail = failingEmail;

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            if (toEmail == _failingEmail)
            {
                throw new InvalidOperationException("Symulowany błąd wysyłki.");
            }
            Sent.Add(toEmail);
            return Task.CompletedTask;
        }
    }
}
