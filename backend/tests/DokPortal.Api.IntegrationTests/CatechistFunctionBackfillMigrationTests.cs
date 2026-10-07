using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Migrations;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Osoby, które już mają misję kanoniczną, dostają funkcję Katechista – raz, bez usuniętych osób i misji.</summary>
public class CatechistFunctionBackfillMigrationTests
{
    // SQLite nie ma NEWID(); w teście wystarczy dowolny unikalny tekst.
    private const string SqliteGuid = "lower(hex(randomblob(16)))";

    private static Person NewPerson(string last, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
        DeletedAtUtc = deleted ? DateTime.UtcNow : null
    };

    private static CanonicalMission NewMission(Person person, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = "Parafia", MissionStartDate = new DateOnly(2025, 9, 1),
        MissionEndDate = new DateOnly(2026, 9, 1), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
        DeletedAtUtc = deleted ? DateTime.UtcNow : null
    };

    [Fact]
    public async Task BackfillSql_AddsTheFunctionOnlyToLivePeopleWithALiveMission_AndOnlyOnce()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var withMission = NewPerson("ZMisja");
        var twoMissions = NewPerson("ZDwiema");
        var alreadyCatechist = NewPerson("Juz");
        var noMission = NewPerson("BezMisji");
        var deletedMission = NewPerson("UsunietaMisja");
        var deletedPerson = NewPerson("UsunietaOsoba", deleted: true);
        db.People.AddRange(withMission, twoMissions, alreadyCatechist, noMission, deletedMission, deletedPerson);
        db.CanonicalMissions.AddRange(
            NewMission(withMission), NewMission(twoMissions), NewMission(twoMissions), NewMission(alreadyCatechist),
            NewMission(deletedMission, deleted: true), NewMission(deletedPerson));
        db.PersonFunctions.Add(new PersonFunction { Id = Guid.NewGuid(), PersonId = alreadyCatechist.Id, Type = FunctionType.Catechist });
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(BackfillCatechistFunctions.BackfillSql(SqliteGuid));
        await db.Database.ExecuteSqlRawAsync(BackfillCatechistFunctions.BackfillSql(SqliteGuid));

        var catechists = await db.PersonFunctions.AsNoTracking().Where(f => f.Type == FunctionType.Catechist)
            .Select(f => f.PersonId).ToListAsync();
        Assert.Equal(
            new[] { withMission.Id, twoMissions.Id, alreadyCatechist.Id }.OrderBy(i => i),
            catechists.OrderBy(i => i));
    }
}
