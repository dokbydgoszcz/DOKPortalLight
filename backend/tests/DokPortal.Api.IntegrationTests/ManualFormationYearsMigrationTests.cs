using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Migrations;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Rok rozpoczęcia formacji (kalendarz) zamienia się w ręcznie prowadzony rok 1–3 i znacznik ukończenia, i z powrotem.</summary>
public class ManualFormationYearsMigrationTests
{
    private static async Task<(SqliteConnection Connection, AppDbContext Db, List<Guid> Ids)> SeedAsync(params int[] startYears)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        var ids = new List<Guid>();
        var updated = new DateTime(2026, 5, 4, 0, 0, 0, DateTimeKind.Utc);
        foreach (var start in startYears)
        {
            var candidate = new Candidate
            {
                Id = Guid.NewGuid(), PersonId = person.Id, FormationYear = start, CreatedAtUtc = updated, UpdatedAtUtc = updated
            };
            ids.Add(candidate.Id);
            db.Candidates.Add(candidate);
        }
        await db.SaveChangesAsync();
        return (connection, db, ids);
    }

    private static async Task<List<(int Year, bool Completed, DateTime Since)>> ReadAsync(AppDbContext db, List<Guid> ids)
    {
        var all = await db.Candidates.AsNoTracking().ToDictionaryAsync(c => c.Id);
        return ids.Select(id => (all[id].FormationYear, all[id].IsFormationCompleted, all[id].FormationYearSinceUtc)).ToList();
    }

    [Fact]
    public async Task ToManualYearsSql_ComputesTheCurrentYear_MarksThoseBeyondYearThreeAsCompleted_AndClampsTheFuture()
    {
        // rok szkolny zaczął się we wrześniu 2026: start 2026 = I rok, 2025 = II, 2024 = III, 2023 = po III, 2027 = jeszcze nie zaczął
        var (connection, db, ids) = await SeedAsync(2026, 2025, 2024, 2023, 2020, 2027);
        using var _ = connection;
        await using var __ = db;

        await db.Database.ExecuteSqlRawAsync(ManualFormationYears.ToManualYearsSql(2026));

        var rows = await ReadAsync(db, ids);
        Assert.Equal(new[] { 1, 2, 3, 3, 3, 1 }, rows.Select(r => r.Year));
        Assert.Equal(new[] { false, false, false, true, true, false }, rows.Select(r => r.Completed));
        Assert.All(rows, r => Assert.Equal(new DateTime(2026, 5, 4, 0, 0, 0, DateTimeKind.Utc), r.Since));
    }

    [Fact]
    public async Task ToStartYearsSql_GoesBack_AndTheRoundTripKeepsTheYearAndTheCompletion()
    {
        var (connection, db, ids) = await SeedAsync(2026, 2025, 2024, 2023);
        using var _ = connection;
        await using var __ = db;
        await db.Database.ExecuteSqlRawAsync(ManualFormationYears.ToManualYearsSql(2026));

        await db.Database.ExecuteSqlRawAsync(ManualFormationYears.ToStartYearsSql(2026));
        var starts = (await ReadAsync(db, ids)).Select(r => r.Year).ToList();
        await db.Database.ExecuteSqlRawAsync(ManualFormationYears.ToManualYearsSql(2026));

        Assert.Equal(new[] { 2026, 2025, 2024, 2023 }, starts);
        Assert.Equal(new[] { 1, 2, 3, 3 }, (await ReadAsync(db, ids)).Select(r => r.Year));
    }
}
