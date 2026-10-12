using DokPortal.Application.Missions;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Misje wygasające i ich odnawianie, kolejność listy Katechiści (przegląd 2026-10-08, A2).</summary>
public class MissionRenewalTests
{
    // „Dziś” w testach: 12 października 2026.
    private static readonly FixedTimeProvider Time = new(2026, 10, 12);
    private static readonly DateOnly Today = new(2026, 10, 12);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Person> AddPersonAsync(AppDbContext db, string last)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person;
    }

    private static async Task<CanonicalMission> AddMissionAsync(AppDbContext db, Person person, DateOnly end, string place = "Fara", Guid? parishId = null)
    {
        var mission = new CanonicalMission
        {
            Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = place, ParishId = parishId, SupervisionGroup = "Grupa A", SentToDok = true,
            MissionStartDate = end.AddYears(-1), MissionEndDate = end, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.CanonicalMissions.Add(mission);
        await db.SaveChangesAsync();
        return mission;
    }

    [Fact]
    public async Task Expiring_ListsMissionsEndingWithin30Days_AndRecentlyExpiredOnesWithoutASuccessor()
    {
        await using var db = CreateContext();
        var soon = await AddMissionAsync(db, await AddPersonAsync(db, "Wkrotce"), Today.AddDays(20));
        var expired = await AddMissionAsync(db, await AddPersonAsync(db, "Wygasla"), Today.AddDays(-40));
        await AddMissionAsync(db, await AddPersonAsync(db, "Daleko"), Today.AddDays(200));
        await AddMissionAsync(db, await AddPersonAsync(db, "Dawno"), Today.AddYears(-2));
        var renewed = await AddPersonAsync(db, "Odnowiona");
        await AddMissionAsync(db, renewed, Today.AddDays(-10));
        await AddMissionAsync(db, renewed, Today.AddDays(355));

        var list = await new MissionService(db, Time).GetExpiringAsync(default);

        Assert.Equal(new[] { expired.Id, soon.Id }, list.Select(m => m.Id));
        Assert.Equal("wygasła", list[0].Status);
        Assert.Equal("wygasa", list[1].Status);
    }

    [Fact]
    public async Task Renew_StartsTheNextYearRightAfterTheOldMission_KeepingPlaceAndGroup()
    {
        await using var db = CreateContext();
        var parish = new Parish { Id = Guid.NewGuid(), Name = "Fara" };
        db.Parishes.Add(parish);
        var person = await AddPersonAsync(db, "Kowalski");
        var old = await AddMissionAsync(db, person, Today.AddDays(10), parishId: parish.Id);

        var renewed = await new MissionService(db, Time).RenewAsync(old.Id, default);

        Assert.NotNull(renewed);
        Assert.NotEqual(old.Id, renewed!.Id);
        Assert.Equal(Today.AddDays(11), renewed.MissionStartDate);
        Assert.Equal(Today.AddDays(11).AddYears(1), renewed.MissionEndDate);
        Assert.Equal(Today, renewed.GrantedDate);
        Assert.Equal("Fara", renewed.ServicePlace);
        Assert.Equal("Grupa A", renewed.SupervisionGroup);
        Assert.True(renewed.SentToDok);
        Assert.Equal(parish.Id, (await db.CanonicalMissions.AsNoTracking().SingleAsync(m => m.Id == renewed.Id)).ParishId);
    }

    [Fact]
    public async Task Renew_ALongExpiredMission_StartsToday_AndAMissionWithASuccessorCannotBeRenewed()
    {
        await using var db = CreateContext();
        var person = await AddPersonAsync(db, "Kowalski");
        var old = await AddMissionAsync(db, person, Today.AddDays(-100));
        var service = new MissionService(db, Time);

        var renewed = await service.RenewAsync(old.Id, default);
        var again = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RenewAsync(old.Id, default));

        Assert.Equal(Today, renewed!.MissionStartDate);
        Assert.Contains("nowszą misję", again.Message);
        Assert.Null(await service.RenewAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task TheList_ShowsValidMissionsFirst_SoonestEndingFirst_ThenExpiredNewestFirst_AndCanBeFiltered()
    {
        await using var db = CreateContext();
        var a = await AddMissionAsync(db, await AddPersonAsync(db, "A"), Today.AddDays(300));
        var b = await AddMissionAsync(db, await AddPersonAsync(db, "B"), Today.AddDays(5));
        var c = await AddMissionAsync(db, await AddPersonAsync(db, "C"), Today.AddDays(-400));
        var d = await AddMissionAsync(db, await AddPersonAsync(db, "D"), Today.AddDays(-3));
        var service = new MissionService(db, Time);

        var all = await service.SearchAsync(null, 1, 20, default);
        var current = await service.SearchAsync(null, 1, 20, default, MissionStatusFilter.Current);
        var expired = await service.SearchAsync(null, 1, 20, default, MissionStatusFilter.Expired);

        Assert.Equal(new[] { b.Id, a.Id, d.Id, c.Id }, all.Items.Select(m => m.Id));
        Assert.Equal(new[] { b.Id, a.Id }, current.Items.Select(m => m.Id));
        Assert.Equal(new[] { d.Id, c.Id }, expired.Items.Select(m => m.Id));
    }
}
