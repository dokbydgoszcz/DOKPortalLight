using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Application.Parishes;
using DokPortal.Application.People;
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

/// <summary>Powiadomienia: nowa sprawa DOK, proboszcz wskazany po skierowaniu, wygasające misje.</summary>
public class NotificationGapsTests
{
    private sealed class Recorder : IEmailSender
    {
        public bool Fail { get; init; }
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            if (Fail) throw new InvalidOperationException("SMTP");
            Sent.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Person NewPerson(string first, string last, string? email = null, string? phone = null) => new()
    {
        Id = Guid.NewGuid(), FirstName = first, LastName = last, Email = email, Phone = phone, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task ANewDokCase_IsAnnouncedToTheCatechistAndTheMentor()
    {
        await using var db = CreateContext();
        var student = NewPerson("Jan", "Kowalski");
        var catechist = NewPerson("Anna", "Maj", "anna@example.org");
        var mentor = NewPerson("Marek", "Lis", "marek@example.org");
        db.People.AddRange(student, catechist, mentor);
        await db.SaveChangesAsync();
        var mail = new Recorder();

        await new DokCaseService(db, emailSender: mail, logger: NullLogger<DokCaseService>.Instance).CreateAsync(new CreateDokCaseRequest
        {
            PersonId = student.Id, Path = DokPath.Confirmation, Stage = DokStage.Evangelization, CatechistPersonId = catechist.Id, MentorPersonId = mentor.Id
        }, default);

        Assert.Equal(new[] { "anna@example.org", "marek@example.org" }, mail.Sent.Select(m => m.To));
        Assert.Equal("Nowa sprawa DOK", mail.Sent[0].Subject);
        Assert.Contains("Jan Kowalski", mail.Sent[0].Body);
        Assert.Contains("katechistą prowadzącym", mail.Sent[0].Body);
        Assert.Contains("mentorem", mail.Sent[1].Body);
    }

    [Fact]
    public async Task ANewDokCase_IsSavedEvenWhenTheMailFails_AndSkipsPeopleWithoutEmail()
    {
        await using var db = CreateContext();
        var student = NewPerson("Jan", "Kowalski");
        var catechist = NewPerson("Anna", "Maj", "anna@example.org");
        var otherCatechist = NewPerson("Ola", "Bez", null);
        db.People.AddRange(student, catechist, otherCatechist);
        await db.SaveChangesAsync();

        var created = await new DokCaseService(db, emailSender: new Recorder { Fail = true }).CreateAsync(new CreateDokCaseRequest
        {
            PersonId = student.Id, Path = DokPath.Confirmation, Stage = DokStage.Evangelization, CatechistPersonId = catechist.Id
        }, default);
        Assert.NotNull(created);

        var mail = new Recorder();
        await new DokCaseService(db, emailSender: mail).CreateAsync(new CreateDokCaseRequest
        {
            PersonId = student.Id, Path = DokPath.Communion, Stage = DokStage.Evangelization, CatechistPersonId = otherCatechist.Id
        }, default);
        Assert.Empty(mail.Sent);
    }

    private static async Task<(Parish Parish, Person Catechist, Person Pastor)> SeedReferralAsync(AppDbContext db, DateTime? assignedAt = null)
    {
        var parish = new Parish { Id = Guid.NewGuid(), Name = "św. Mateusza" };
        var catechist = NewPerson("Anna", "Maj", "anna@example.org", "600 100 200");
        var pastor = NewPerson("Jan", "Proboszcz", "proboszcz@example.org", "52 000 00 00");
        var need = new ParishNeed { Id = Guid.NewGuid(), ParishId = parish.Id, Description = "Katecheza dorosłych", CreatedAtUtc = DateTime.UtcNow };
        db.Parishes.Add(parish);
        db.People.AddRange(catechist, pastor);
        db.ParishNeeds.Add(need);
        db.ParishNeedAssignments.Add(new ParishNeedAssignment { Id = Guid.NewGuid(), ParishNeedId = need.Id, PersonId = catechist.Id, AssignedAtUtc = assignedAt ?? DateTime.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();
        return (parish, catechist, pastor);
    }

    [Fact]
    public async Task APastorChosenAfterTheReferral_GetsTheCatechistsAndTheyGetThePastor()
    {
        await using var db = CreateContext();
        var (parish, catechist, pastor) = await SeedReferralAsync(db);
        var mail = new Recorder();
        var service = new ParishService(db, mail, NullLogger<ParishService>.Instance);

        await service.UpdateAsync(parish.Id, new UpdateParishRequest { Name = parish.Name, City = null, PastorPersonId = pastor.Id }, default);

        Assert.Equal(2, mail.Sent.Count);
        var toPastor = mail.Sent.Single(m => m.To == "proboszcz@example.org");
        Assert.Contains("Anna Maj (zapotrzebowanie: Katecheza dorosłych)", toPastor.Body);
        Assert.Contains("600 100 200", toPastor.Body);
        var toCatechist = mail.Sent.Single(m => m.To == "anna@example.org");
        Assert.Contains("Jan Proboszcz", toCatechist.Body);
        Assert.Contains("52 000 00 00", toCatechist.Body);

        // Ten sam proboszcz zapisany ponownie – bez powtórnych e-maili.
        await service.UpdateAsync(parish.Id, new UpdateParishRequest { Name = parish.Name, City = null, PastorPersonId = pastor.Id }, default);
        Assert.Equal(2, mail.Sent.Count);
    }

    [Fact]
    public async Task OldReferrals_AndParishesWithoutReferrals_AreNotMailed()
    {
        await using var db = CreateContext();
        var (parish, _, pastor) = await SeedReferralAsync(db, DateTime.UtcNow.AddYears(-2));
        var mail = new Recorder();

        await new ParishService(db, mail).UpdateAsync(parish.Id, new UpdateParishRequest { Name = parish.Name, City = null, PastorPersonId = pastor.Id }, default);
        await new ParishService(db, mail).CreateAsync(new CreateParishRequest { Name = "Nowa", City = null, PastorPersonId = null }, default);

        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task APastorFunctionSetOnThePersonCard_AlsoNotifies_AndMailFailuresDoNotBlockSaving()
    {
        await using var db = CreateContext();
        var (parish, _, pastor) = await SeedReferralAsync(db);
        var mail = new Recorder();
        var request = new UpdatePersonRequest
        {
            FirstName = pastor.FirstName, LastName = pastor.LastName, Email = pastor.Email, Phone = pastor.Phone,
            Functions = new[] { new PersonFunctionInput { Type = FunctionType.Pastor, ParishId = parish.Id } }
        };

        await new PersonService(db, emailSender: mail).UpdateAsync(pastor.Id, request, default);
        Assert.Equal(2, mail.Sent.Count);
        await new PersonService(db, emailSender: mail).UpdateAsync(pastor.Id, request, default);
        Assert.Equal(2, mail.Sent.Count);

        var (otherParish, _, _) = await SeedReferralAsync(db);
        var newPastor = await new PersonService(db, emailSender: new Recorder { Fail = true }).CreateAsync(new CreatePersonRequest
        {
            FirstName = "Piotr", LastName = "Nowy", Email = "nowy@example.org",
            Functions = new[] { new PersonFunctionInput { Type = FunctionType.Pastor, ParishId = otherParish.Id } }
        }, default);
        Assert.Equal("Piotr Nowy", newPastor.FullName);
    }

    private static CanonicalMission Mission(Person person, DateOnly end) => new()
    {
        Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = "św. Mateusza", MissionStartDate = end.AddYears(-1), MissionEndDate = end,
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task ExpiringMissions_AreRemindedOnce_ToTheCatechistAndTheMissionManagers()
    {
        await using var db = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soon = NewPerson("Anna", "Maj", "anna@example.org");
        var renewed = NewPerson("Ewa", "Lis", "ewa@example.org");
        var later = NewPerson("Ola", "Kot", "ola@example.org");
        var noEmail = NewPerson("Bez", "Maila");
        db.People.AddRange(soon, renewed, later, noEmail);
        db.CanonicalMissions.AddRange(
            Mission(soon, today.AddDays(20)),
            Mission(renewed, today.AddDays(10)), Mission(renewed, today.AddDays(375)),
            Mission(later, today.AddDays(60)),
            Mission(noEmail, today.AddDays(5)));
        var adminRole = new IdentityRole(AppRoles.Administrator) { NormalizedName = AppRoles.Administrator.ToUpperInvariant() };
        var sksp = new IdentityRole(AppRoles.DyrektorSKSP) { NormalizedName = AppRoles.DyrektorSKSP.ToUpperInvariant() };
        var other = new IdentityRole(AppRoles.Superwizor) { NormalizedName = AppRoles.Superwizor.ToUpperInvariant() };
        db.Roles.AddRange(adminRole, sksp, other);
        db.RolePermissions.Add(new RolePermission { RoleName = AppRoles.DyrektorSKSP, Permission = Permissions.MissionsManage });
        var admin = new AppUser { Id = "a", UserName = "admin@example.org", Email = "admin@example.org" };
        var director = new AppUser { Id = "d", UserName = "sksp@example.org", Email = "sksp@example.org" };
        var supervisor = new AppUser { Id = "s", UserName = "sup@example.org", Email = "sup@example.org" };
        db.Users.AddRange(admin, director, supervisor);
        db.UserRoles.AddRange(
            new IdentityUserRole<string> { UserId = "a", RoleId = adminRole.Id },
            new IdentityUserRole<string> { UserId = "d", RoleId = sksp.Id },
            new IdentityUserRole<string> { UserId = "s", RoleId = other.Id });
        await db.SaveChangesAsync();
        var mail = new Recorder();
        var service = new ReminderService(db, mail, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunExpiringMissionsReminderAsync(default);

        Assert.Equal((2, 1, 2, 0), (result.MissionsFound, result.CatechistsNotified, result.ManagersNotified, result.FailedSends));
        Assert.Contains(mail.Sent, m => m.To == "anna@example.org" && m.Body.Contains(today.AddDays(20).ToString("dd.MM.yyyy")));
        var digest = mail.Sent.Single(m => m.To == "sksp@example.org").Body;
        Assert.Contains("Anna Maj", digest);
        Assert.Contains("Bez Maila", digest);
        Assert.DoesNotContain("Ewa Lis", digest);
        Assert.DoesNotContain(mail.Sent, m => m.To == "sup@example.org");

        var again = await service.RunExpiringMissionsReminderAsync(default);
        Assert.Equal(0, again.MissionsFound);
    }

    [Fact]
    public async Task ExpiringMissions_CountFailedSends()
    {
        await using var db = CreateContext();
        var person = NewPerson("Anna", "Maj", "anna@example.org");
        db.People.Add(person);
        db.CanonicalMissions.Add(Mission(person, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3)));
        var adminRole = new IdentityRole(AppRoles.Administrator) { NormalizedName = AppRoles.Administrator.ToUpperInvariant() };
        db.Roles.Add(adminRole);
        db.Users.Add(new AppUser { Id = "a", UserName = "admin@example.org", Email = "admin@example.org" });
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "a", RoleId = adminRole.Id });
        await db.SaveChangesAsync();

        var result = await new ReminderService(db, new Recorder { Fail = true }, NullLogger<ReminderService>.Instance, new NameDayService(db))
            .RunExpiringMissionsReminderAsync(default);

        Assert.Equal((1, 0, 0, 2), (result.MissionsFound, result.CatechistsNotified, result.ManagersNotified, result.FailedSends));
    }
}
