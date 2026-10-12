using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Migrations;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Naprawa danych po dawnym usuwaniu osób i parafii bez sprawdzania zależności (migracja LinkMissionsToParishesAndAssignments).</summary>
public class RepairDeletedPeopleMigrationTests
{
    private const string SqliteNow = "datetime('now')";

    private static Person NewPerson(string last, bool deleted = false, Guid? parishId = null) => new()
    {
        Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, ParishId = parishId,
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, DeletedAtUtc = deleted ? DateTime.UtcNow : null
    };

    private static DokCase NewCase(Person student, Person catechist, Person? mentor = null) => new()
    {
        Id = Guid.NewGuid(), PersonId = student.Id, CatechistPersonId = catechist.Id, MentorPersonId = mentor?.Id,
        Path = DokPath.Confirmation, Stage = DokStage.Evangelization, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static CanonicalMission NewMission(Person person, string place) => new()
    {
        Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = place, MissionStartDate = new DateOnly(2026, 1, 1),
        MissionEndDate = new DateOnly(2027, 1, 1), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static async Task<(SqliteConnection, AppDbContext)> OpenAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return (connection, db);
    }

    private static async Task RunRepairAsync(AppDbContext db)
    {
        foreach (var sql in LinkMissionsToParishesAndAssignments.RepairSql(SqliteNow))
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Repair_RemovesTheDeletedStudentsCase_RestoresTheDeletedCatechist_AndClearsMentorAndAccount()
    {
        var (connection, db) = await OpenAsync();
        await using var _ = connection;
        await using var __ = db;
        var deletedStudent = NewPerson("UsunietyPodopieczny", deleted: true);
        var liveStudent = NewPerson("Podopieczny");
        var deletedCatechist = NewPerson("UsunietyKatechista", deleted: true);
        var liveCatechist = NewPerson("Katechista");
        var deletedMentor = NewPerson("UsunietyMentor", deleted: true);
        db.People.AddRange(deletedStudent, liveStudent, deletedCatechist, liveCatechist, deletedMentor);
        var orphan = NewCase(deletedStudent, liveCatechist);
        var hidden = NewCase(liveStudent, deletedCatechist, deletedMentor);
        db.DokCases.AddRange(orphan, hidden);
        var mission = NewMission(deletedStudent, "x");
        db.CanonicalMissions.Add(mission);
        db.Users.Add(new AppUser { Id = Guid.NewGuid().ToString(), UserName = "u@example.org", Email = "u@example.org", PersonId = deletedMentor.Id });
        await db.SaveChangesAsync();

        await RunRepairAsync(db);

        var cases = await db.DokCases.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        Assert.NotNull(cases.Single(c => c.Id == orphan.Id).DeletedAtUtc);
        var repaired = cases.Single(c => c.Id == hidden.Id);
        Assert.Null(repaired.DeletedAtUtc);
        Assert.Null(repaired.MentorPersonId);
        Assert.Null((await db.People.IgnoreQueryFilters().SingleAsync(p => p.Id == deletedCatechist.Id)).DeletedAtUtc);
        Assert.NotNull((await db.People.IgnoreQueryFilters().SingleAsync(p => p.Id == deletedMentor.Id)).DeletedAtUtc);
        Assert.NotNull((await db.CanonicalMissions.IgnoreQueryFilters().SingleAsync(m => m.Id == mission.Id)).DeletedAtUtc);
        Assert.Null((await db.Users.SingleAsync()).PersonId);
        Assert.Single(await db.DokCases.ToListAsync());
    }

    [Fact]
    public async Task Repair_ClearsReferencesToDeletedParishes_AndBackfillLinksMissionsByUniqueParishName()
    {
        var (connection, db) = await OpenAsync();
        await using var _ = connection;
        await using var __ = db;
        var deletedParish = new Parish { Id = Guid.NewGuid(), Name = "Stara", DeletedAtUtc = DateTime.UtcNow };
        var parish = new Parish { Id = Guid.NewGuid(), Name = "Fara" };
        var twinA = new Parish { Id = Guid.NewGuid(), Name = "Bliźniacza" };
        var twinB = new Parish { Id = Guid.NewGuid(), Name = "Bliźniacza" };
        db.Parishes.AddRange(deletedParish, parish, twinA, twinB);
        var parishioner = NewPerson("Parafianin", parishId: deletedParish.Id);
        db.People.Add(parishioner);
        var pastor = new PersonFunction { Id = Guid.NewGuid(), PersonId = parishioner.Id, Type = FunctionType.Pastor, ParishId = deletedParish.Id };
        db.PersonFunctions.Add(pastor);
        var need = new ParishNeed { Id = Guid.NewGuid(), ParishId = deletedParish.Id, Description = "x", CreatedAtUtc = DateTime.UtcNow };
        db.ParishNeeds.Add(need);
        var inFara = NewMission(parishioner, "Fara");
        var inTwin = NewMission(parishioner, "Bliźniacza");
        var elsewhere = NewMission(parishioner, "Szkoła");
        db.CanonicalMissions.AddRange(inFara, inTwin, elsewhere);
        await db.SaveChangesAsync();

        await RunRepairAsync(db);
        await db.Database.ExecuteSqlRawAsync(LinkMissionsToParishesAndAssignments.BackfillMissionParishSql);
        await db.Database.ExecuteSqlRawAsync(LinkMissionsToParishesAndAssignments.BackfillMissionParishSql);

        Assert.Null((await db.People.AsNoTracking().SingleAsync()).ParishId);
        Assert.Null((await db.PersonFunctions.AsNoTracking().SingleAsync()).ParishId);
        Assert.NotNull((await db.ParishNeeds.IgnoreQueryFilters().AsNoTracking().SingleAsync()).DeletedAtUtc);
        var missions = await db.CanonicalMissions.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.ParishId);
        Assert.Equal(parish.Id, missions[inFara.Id]);
        Assert.Null(missions[inTwin.Id]);
        Assert.Null(missions[elsewhere.Id]);
    }
}
