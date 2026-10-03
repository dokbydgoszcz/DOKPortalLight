using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Migrations;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Kolumna Year (rok formacji 1–3) zamienia się w rok rozpoczęcia formacji i z powrotem.</summary>
public class CandidateFormationCalendarMigrationTests
{
    private static async Task<(SqliteConnection Connection, AppDbContext Db, List<Guid> Ids)> SeedAsync(params int[] values)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        var ids = new List<Guid>();
        foreach (var value in values)
        {
            var candidate = new Candidate
            {
                Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = value, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            ids.Add(candidate.Id);
            db.Candidates.Add(candidate);
        }
        await db.SaveChangesAsync();
        return (connection, db, ids);
    }

    private static async Task<List<int>> ReadAsync(AppDbContext db, List<Guid> ids)
    {
        var all = await db.Candidates.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.FormationStartYear);
        return ids.Select(id => all[id]).ToList();
    }

    [Fact]
    public async Task ToStartYearSql_TurnsTheFormationYearIntoTheYearTheFormationStarted()
    {
        var (connection, db, ids) = await SeedAsync(1, 2, 3);
        using var _ = connection;
        await using var __ = db;

        await db.Database.ExecuteSqlRawAsync(AddCandidateFormationCalendar.ToStartYearSql(2026));

        Assert.Equal(new[] { 2026, 2025, 2024 }, await ReadAsync(db, ids));
    }

    [Fact]
    public async Task ToFormationYearSql_GoesBack_ClampingToOneThroughThree()
    {
        var (connection, db, ids) = await SeedAsync(2026, 2025, 2024, 2020, 2027);
        using var _ = connection;
        await using var __ = db;

        await db.Database.ExecuteSqlRawAsync(AddCandidateFormationCalendar.ToFormationYearSql(2026));

        Assert.Equal(new[] { 1, 2, 3, 3, 1 }, await ReadAsync(db, ids));
    }

    [Fact]
    public async Task TheRoundTrip_KeepsTheYear()
    {
        var (connection, db, ids) = await SeedAsync(1, 2, 3);
        using var _ = connection;
        await using var __ = db;

        await db.Database.ExecuteSqlRawAsync(AddCandidateFormationCalendar.ToStartYearSql(2026));
        await db.Database.ExecuteSqlRawAsync(AddCandidateFormationCalendar.ToFormationYearSql(2026));

        Assert.Equal(new[] { 1, 2, 3 }, await ReadAsync(db, ids));
    }
}
