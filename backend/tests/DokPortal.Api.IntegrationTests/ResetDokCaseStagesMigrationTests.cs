using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Migrations;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Istniejące sprawy wracają na pierwszy etap swojej ścieżki; absolwenci zostają absolwentami.</summary>
public class ResetDokCaseStagesMigrationTests
{
    private static async Task<(SqliteConnection Connection, AppDbContext Db, Dictionary<string, Guid> Ids)> SeedAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(person, catechist);
        var ids = new Dictionary<string, Guid>();
        // (ścieżka, dawny etap: 0 Zgłoszenie, 1 Formacja, 2 Sakrament, 3 Absolwent)
        foreach (var (name, path, oldStage) in new[]
        {
            ("baptism-formation", DokPath.BaptismCandidate, 1),
            ("baptism-sacrament", DokPath.BaptismCandidate, 2),
            ("baptism-graduate", DokPath.BaptismCandidate, 3),
            ("confirmation-application", DokPath.Confirmation, 0),
            ("communion-sacrament", DokPath.Communion, 2),
            ("conversion-graduate", DokPath.Conversion, 3),
            ("return-formation", DokPath.ReturnToUnity, 1)
        })
        {
            var dokCase = new DokCase
            {
                Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id, Path = path, Stage = (DokStage)oldStage,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            ids[name] = dokCase.Id;
            db.DokCases.Add(dokCase);
        }
        await db.SaveChangesAsync();
        return (connection, db, ids);
    }

    private static async Task<Dictionary<string, DokStage>> StagesAsync(AppDbContext db, Dictionary<string, Guid> ids)
    {
        var all = await db.DokCases.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Stage);
        return ids.ToDictionary(p => p.Key, p => all[p.Value]);
    }

    [Fact]
    public async Task ResetSql_PutsEveryoneOnTheFirstStageOfThePath_ButKeepsGraduates()
    {
        var (connection, db, ids) = await SeedAsync();
        using var _ = connection;
        await using var __ = db;

        await db.Database.ExecuteSqlRawAsync(ResetDokCaseStages.ResetSql);

        var stages = await StagesAsync(db, ids);
        Assert.Equal(DokStage.Prekatechumenate, stages["baptism-formation"]);
        Assert.Equal(DokStage.Prekatechumenate, stages["baptism-sacrament"]);
        Assert.Equal(DokStage.Graduate, stages["baptism-graduate"]);
        Assert.Equal(DokStage.Evangelization, stages["confirmation-application"]);
        Assert.Equal(DokStage.Evangelization, stages["communion-sacrament"]);
        Assert.Equal(DokStage.Graduate, stages["conversion-graduate"]);
        Assert.Equal(DokStage.Evangelization, stages["return-formation"]);
    }

    [Fact]
    public async Task ResetSql_EveryResultIsAValidStageOfItsPath()
    {
        var (connection, db, _) = await SeedAsync();
        using var __ = connection;
        await using var ___ = db;

        await db.Database.ExecuteSqlRawAsync(ResetDokCaseStages.ResetSql);

        var cases = await db.DokCases.AsNoTracking().ToListAsync();
        Assert.All(cases, c => Assert.True(DokPortal.Domain.Formation.DokStages.IsValid(c.Path, c.Stage), $"{c.Path}/{c.Stage}"));
    }

    [Fact]
    public async Task RestoreSql_ReturnsToTheOldNumbers_KeepingGraduates()
    {
        var (connection, db, ids) = await SeedAsync();
        using var _ = connection;
        await using var __ = db;
        await db.Database.ExecuteSqlRawAsync(ResetDokCaseStages.ResetSql);

        await db.Database.ExecuteSqlRawAsync(ResetDokCaseStages.RestoreSql);

        var stages = await StagesAsync(db, ids);
        Assert.Equal(DokStage.Graduate, stages["baptism-graduate"]);
        Assert.Equal((DokStage)0, stages["baptism-formation"]);
    }
}

public class AddDokCaseStageSinceMigrationTests
{
    [Fact]
    public async Task BackfillSql_StartsGraduatesAtTheirCompletion_AndEveryoneElseAtTheCreationOfTheCase()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var created = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var completed = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var open = new DokCase { Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id, Path = DokPath.Confirmation, Stage = DokStage.Evangelization, CreatedAtUtc = created, UpdatedAtUtc = DateTime.UtcNow, StageSinceUtc = DateTime.UtcNow };
        var graduate = new DokCase { Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id, Path = DokPath.Confirmation, Stage = DokStage.Graduate, CreatedAtUtc = created, CompletedAtUtc = completed, UpdatedAtUtc = DateTime.UtcNow, StageSinceUtc = DateTime.UtcNow };
        db.People.AddRange(person, catechist);
        db.DokCases.AddRange(open, graduate);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(AddDokCaseStageSince.BackfillSql);

        var since = await db.DokCases.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.StageSinceUtc);
        Assert.Equal(created, since[open.Id]);
        Assert.Equal(completed, since[graduate.Id]);
    }
}
