using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Migrations;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>
/// Dane dotychczasowych kolumn (flaga rekolekcji, jedna przypisana osoba) trafiają do nowych tabel, a w drugą stronę wracają.
/// Starsze kolumny są dodawane ręcznie, bo bieżący model ich już nie ma.
/// </summary>
public class CandidateRetreatsMigrationTests
{
    private static async Task<(SqliteConnection Connection, AppDbContext Db)> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Candidates ADD COLUMN IsRetreatCompleted INTEGER NOT NULL DEFAULT 0");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE ParishNeeds ADD COLUMN AssignedPersonId TEXT NULL");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE ParishNeeds ADD COLUMN AssignedAtUtc TEXT NULL");
        return (connection, db);
    }

    private static Person NewPerson(string last) =>
        new() { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };

    [Fact]
    public async Task RetreatsUpSql_CreatesACompletedRetreatForTheCurrentYear_OnlyForCandidatesWithTheFlag()
    {
        var (connection, db) = await CreateAsync();
        using var _ = connection;
        await using var __ = db;
        var person = NewPerson("Kowalski");
        db.People.Add(person);
        var done = new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, Year = 2, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var pending = new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, Year = 3, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.Candidates.AddRange(done, pending);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE Candidates SET IsRetreatCompleted = 1 WHERE Id = {0}", done.Id.ToString().ToUpper());

        await db.Database.ExecuteSqlRawAsync(AddCandidateRetreatsAndNeedAssignments.RetreatsUpSql);

        var retreat = await db.CandidateRetreats.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((done.Id, 2, true), (retreat.CandidateId, retreat.Year, retreat.IsCompleted));
    }

    [Fact]
    public async Task AssignmentsUpSql_MovesTheSingleAssignedPerson_KeepingTheDate_AndFallingBackToTheCreationDate()
    {
        var (connection, db) = await CreateAsync();
        using var _ = connection;
        await using var __ = db;
        var parish = new Parish { Id = Guid.NewGuid(), Name = "św. Jana" };
        var marek = NewPerson("Zielinski");
        var created = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var open = new ParishNeed { Id = Guid.NewGuid(), ParishId = parish.Id, Description = "Otwarte", CreatedAtUtc = created };
        var assigned = new ParishNeed { Id = Guid.NewGuid(), ParishId = parish.Id, Description = "Skierowane", CreatedAtUtc = created };
        var legacy = new ParishNeed { Id = Guid.NewGuid(), ParishId = parish.Id, Description = "Bez daty", CreatedAtUtc = created };
        db.Parishes.Add(parish);
        db.People.Add(marek);
        db.ParishNeeds.AddRange(open, assigned, legacy);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE ParishNeeds SET AssignedPersonId = {0}, AssignedAtUtc = '2026-09-10 12:30:00' WHERE Id = {1}",
            marek.Id.ToString().ToUpper(), assigned.Id.ToString().ToUpper());
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE ParishNeeds SET AssignedPersonId = {0} WHERE Id = {1}", marek.Id.ToString().ToUpper(), legacy.Id.ToString().ToUpper());

        await db.Database.ExecuteSqlRawAsync(AddCandidateRetreatsAndNeedAssignments.AssignmentsUpSql);

        var rows = await db.ParishNeedAssignments.IgnoreQueryFilters().OrderBy(a => a.AssignedAtUtc).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(created, rows[0].AssignedAtUtc);
        Assert.Equal(legacy.Id, rows[0].ParishNeedId);
        Assert.Equal(new DateTime(2026, 9, 10, 12, 30, 0), rows[1].AssignedAtUtc);
        Assert.Equal((assigned.Id, marek.Id), (rows[1].ParishNeedId, rows[1].PersonId));
    }

    [Fact]
    public async Task DownSql_RestoresTheFlagForTheCurrentYear_AndTheFirstAssignedPerson()
    {
        var (connection, db) = await CreateAsync();
        using var _ = connection;
        await using var __ = db;
        var parish = new Parish { Id = Guid.NewGuid(), Name = "św. Jana" };
        var first = NewPerson("Zielinski");
        var second = NewPerson("Maj");
        var candidate = new Candidate { Id = Guid.NewGuid(), PersonId = first.Id, Year = 2, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var need = new ParishNeed { Id = Guid.NewGuid(), ParishId = parish.Id, Description = "x", CreatedAtUtc = DateTime.UtcNow };
        db.Parishes.Add(parish);
        db.People.AddRange(first, second);
        db.Candidates.Add(candidate);
        db.ParishNeeds.Add(need);
        db.CandidateRetreats.AddRange(
            new CandidateRetreat { Id = Guid.NewGuid(), CandidateId = candidate.Id, Year = 1, IsCompleted = false },
            new CandidateRetreat { Id = Guid.NewGuid(), CandidateId = candidate.Id, Year = 2, IsCompleted = true });
        db.ParishNeedAssignments.AddRange(
            new ParishNeedAssignment { Id = Guid.NewGuid(), ParishNeedId = need.Id, PersonId = second.Id, AssignedAtUtc = new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc) },
            new ParishNeedAssignment { Id = Guid.NewGuid(), ParishNeedId = need.Id, PersonId = first.Id, AssignedAtUtc = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(AddCandidateRetreatsAndNeedAssignments.RetreatsDownSql);
        await db.Database.ExecuteSqlRawAsync(AddCandidateRetreatsAndNeedAssignments.AssignmentsDownSql);

        var flag = await db.Database.SqlQueryRaw<int>("SELECT IsRetreatCompleted AS Value FROM Candidates").SingleAsync();
        var person = await db.Database.SqlQueryRaw<string>("SELECT AssignedPersonId AS Value FROM ParishNeeds").SingleAsync();
        Assert.Equal(1, flag);
        Assert.Equal(first.Id.ToString().ToUpper(), person.ToUpper());
    }
}
