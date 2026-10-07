using DokPortal.Application.Common;
using DokPortal.Application.ParishNeeds;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Po „Skieruj” do parafii powstaje rekord misji kanonicznej, a proboszcz i katechista dostają e-mail z wzajemnymi danymi kontaktowymi.</summary>
public class ParishNeedAssignmentFollowUpTests
{
    private static readonly FixedTimeProvider Time = new(2026, 10, 3);

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            Sent.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct) =>
            throw new InvalidOperationException("SMTP nie działa");
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Person NewPerson(string first, string last, string? email = null, string? phone = null) => new()
    {
        Id = Guid.NewGuid(), FirstName = first, LastName = last, Email = email, Phone = phone,
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private record Fixture(AppDbContext Db, Parish Parish, Person Catechist, ParishNeedDto Need, Func<IEmailSender?, ParishNeedService> ServiceWith);

    private static async Task<Fixture> SeedAsync(bool withPastor = true, string? catechistEmail = "anna@example.org", string? pastorEmail = "proboszcz@example.org")
    {
        var db = CreateContext();
        var parish = new Parish { Id = Guid.NewGuid(), Name = "św. Mateusza" };
        var catechist = NewPerson("Anna", "Maj", catechistEmail, "600 100 200");
        db.Parishes.Add(parish);
        db.People.Add(catechist);
        if (withPastor)
        {
            var pastor = NewPerson("Ks. Adam", "Pierwszy", pastorEmail, "500 300 400");
            db.People.Add(pastor);
            db.PersonFunctions.Add(new PersonFunction { Id = Guid.NewGuid(), PersonId = pastor.Id, Type = FunctionType.Pastor, ParishId = parish.Id });
        }
        await db.SaveChangesAsync();
        ParishNeedService ServiceWith(IEmailSender? email) => new(db, email, Time);
        var need = await ServiceWith(null).CreateAsync(new CreateParishNeedRequest { ParishId = parish.Id, Description = "Katechista do przygotowania dorosłych" }, default);
        return new Fixture(db, parish, catechist, need, ServiceWith);
    }

    private static Task<List<CanonicalMission>> MissionsOfAsync(AppDbContext db, Guid personId) =>
        db.CanonicalMissions.AsNoTracking().Where(m => m.PersonId == personId).ToListAsync();

    [Fact]
    public async Task Assigning_CreatesAOneYearMissionInTheParish_AndMakesThePersonACatechist()
    {
        var f = await SeedAsync();

        await f.ServiceWith(null).AssignAsync(f.Need.Id, f.Catechist.Id, default);

        var mission = Assert.Single(await MissionsOfAsync(f.Db, f.Catechist.Id));
        Assert.Equal(("św. Mateusza", new DateOnly(2026, 10, 3), new DateOnly(2027, 10, 3)), (mission.ServicePlace, mission.MissionStartDate, mission.MissionEndDate));
        Assert.Null(mission.GrantedDate);
        Assert.Contains(FunctionType.Catechist, await f.Db.PersonFunctions.AsNoTracking().Where(x => x.PersonId == f.Catechist.Id).Select(x => x.Type).ToListAsync());
    }

    [Fact]
    public async Task Assigning_FillsInThePlaceOfAMissionGrantedEarlier_InsteadOfAddingASecondOne()
    {
        var f = await SeedAsync();
        f.Db.CanonicalMissions.Add(new CanonicalMission
        {
            Id = Guid.NewGuid(), PersonId = f.Catechist.Id, ServicePlace = "", MissionStartDate = new DateOnly(2026, 9, 1),
            MissionEndDate = new DateOnly(2027, 9, 1), GrantedDate = new DateOnly(2026, 9, 1), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();

        await f.ServiceWith(null).AssignAsync(f.Need.Id, f.Catechist.Id, default);

        var mission = Assert.Single(await MissionsOfAsync(f.Db, f.Catechist.Id));
        Assert.Equal("św. Mateusza", mission.ServicePlace);
        Assert.Equal(new DateOnly(2026, 9, 1), mission.GrantedDate);
    }

    [Fact]
    public async Task AssigningTheSamePersonAgain_OrToAnotherNeedOfTheSameParish_DoesNotDuplicateTheMission()
    {
        var f = await SeedAsync();
        var service = f.ServiceWith(null);
        var another = await service.CreateAsync(new CreateParishNeedRequest { ParishId = f.Parish.Id, Description = "Drugie" }, default);

        await service.AssignAsync(f.Need.Id, f.Catechist.Id, default);
        await service.AssignAsync(f.Need.Id, f.Catechist.Id, default);
        await service.AssignAsync(another.Id, f.Catechist.Id, default);

        Assert.Single(await MissionsOfAsync(f.Db, f.Catechist.Id));
    }

    [Fact]
    public async Task AMissionElsewhere_DoesNotBlockANewMissionInThisParish()
    {
        var f = await SeedAsync();
        f.Db.CanonicalMissions.Add(new CanonicalMission
        {
            Id = Guid.NewGuid(), PersonId = f.Catechist.Id, ServicePlace = "św. Jakuba", MissionStartDate = new DateOnly(2026, 1, 1),
            MissionEndDate = new DateOnly(2027, 1, 1), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();

        await f.ServiceWith(null).AssignAsync(f.Need.Id, f.Catechist.Id, default);

        Assert.Equal(new[] { "św. Jakuba", "św. Mateusza" }, (await MissionsOfAsync(f.Db, f.Catechist.Id)).Select(m => m.ServicePlace).OrderBy(x => x));
    }

    [Fact]
    public async Task BothSidesGetAnEmail_WithTheOthersContactDetails()
    {
        var f = await SeedAsync();
        var sender = new RecordingEmailSender();

        await f.ServiceWith(sender).AssignAsync(f.Need.Id, f.Catechist.Id, default);

        Assert.Equal(new[] { "anna@example.org", "proboszcz@example.org" }, sender.Sent.Select(s => s.To).OrderBy(x => x));
        var toPastor = sender.Sent.Single(s => s.To == "proboszcz@example.org");
        Assert.Contains("Anna Maj", toPastor.Body);
        Assert.Contains("anna@example.org", toPastor.Body);
        Assert.Contains("600 100 200", toPastor.Body);
        Assert.Contains("św. Mateusza", toPastor.Subject);
        var toCatechist = sender.Sent.Single(s => s.To == "anna@example.org");
        Assert.Contains("Ks. Adam Pierwszy", toCatechist.Body);
        Assert.Contains("proboszcz@example.org", toCatechist.Body);
        Assert.Contains("500 300 400", toCatechist.Body);
        Assert.Contains("Katechista do przygotowania dorosłych", toCatechist.Body);
    }

    [Fact]
    public async Task WithoutAPastor_OnlyTheCatechistIsMailed_AndToldThereIsNoPastorYet()
    {
        var f = await SeedAsync(withPastor: false);
        var sender = new RecordingEmailSender();

        await f.ServiceWith(sender).AssignAsync(f.Need.Id, f.Catechist.Id, default);

        var sent = Assert.Single(sender.Sent);
        Assert.Equal("anna@example.org", sent.To);
        Assert.Contains("nie ma jeszcze przypisanego proboszcza", sent.Body);
    }

    [Fact]
    public async Task SidesWithoutAnEmailAddress_AreSkipped()
    {
        var f = await SeedAsync(catechistEmail: null, pastorEmail: null);
        var sender = new RecordingEmailSender();

        await f.ServiceWith(sender).AssignAsync(f.Need.Id, f.Catechist.Id, default);

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task APastorOfAnotherParish_IsNotMailed()
    {
        var f = await SeedAsync(withPastor: false);
        var other = new Parish { Id = Guid.NewGuid(), Name = "św. Pawła" };
        var stranger = NewPerson("Ks. Piotr", "Obcy", "obcy@example.org");
        f.Db.Parishes.Add(other);
        f.Db.People.Add(stranger);
        f.Db.PersonFunctions.Add(new PersonFunction { Id = Guid.NewGuid(), PersonId = stranger.Id, Type = FunctionType.Pastor, ParishId = other.Id });
        await f.Db.SaveChangesAsync();
        var sender = new RecordingEmailSender();

        await f.ServiceWith(sender).AssignAsync(f.Need.Id, f.Catechist.Id, default);

        Assert.DoesNotContain(sender.Sent, s => s.To == "obcy@example.org");
    }

    [Fact]
    public async Task AFailingMailServer_DoesNotStopTheAssignment()
    {
        var f = await SeedAsync();

        var result = await f.ServiceWith(new FailingEmailSender()).AssignAsync(f.Need.Id, f.Catechist.Id, default);

        Assert.Equal("Assigned", result!.Status);
        Assert.Single(await MissionsOfAsync(f.Db, f.Catechist.Id));
    }

    [Fact]
    public async Task NoMailIsSent_WhenThePersonWasAlreadyAssigned()
    {
        var f = await SeedAsync();
        await f.ServiceWith(null).AssignAsync(f.Need.Id, f.Catechist.Id, default);
        var sender = new RecordingEmailSender();

        await f.ServiceWith(sender).AssignAsync(f.Need.Id, f.Catechist.Id, default);

        Assert.Empty(sender.Sent);
    }
}
