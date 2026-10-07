using DokPortal.Application.DokCases;
using DokPortal.Application.Dashboard;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Etap formacji musi pasować do ścieżki podopiecznego.</summary>
public class DokStageRulesTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Person NewPerson(string last) =>
        new() { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };

    private static async Task<(AppDbContext Db, DokCaseService Service, Guid PersonId, Guid CatechistId)> SeedAsync()
    {
        var db = CreateContext();
        var person = NewPerson("Kowalski");
        var catechist = NewPerson("Maj");
        db.People.AddRange(person, catechist);
        await db.SaveChangesAsync();
        return (db, new DokCaseService(db), person.Id, catechist.Id);
    }

    private static CreateDokCaseRequest Create(Guid personId, Guid catechistId, DokPath path, DokStage stage) =>
        new() { PersonId = personId, Path = path, Stage = stage, CatechistPersonId = catechistId };

    [Theory]
    [InlineData(DokPath.BaptismCandidate, DokStage.Catechumenate)]
    [InlineData(DokPath.Confirmation, DokStage.Evangelization)]
    [InlineData(DokPath.Communion, DokStage.CloserFormation)]
    [InlineData(DokPath.Conversion, DokStage.Evangelization)]
    [InlineData(DokPath.ReturnToUnity, DokStage.CloserFormation)]
    public async Task CreateAsync_AcceptsAStageOfThePath(DokPath path, DokStage stage)
    {
        var (db, service, personId, catechistId) = await SeedAsync();
        await using var _ = db;

        var created = await service.CreateAsync(Create(personId, catechistId, path, stage), default);

        Assert.Equal(stage, created.Stage);
    }

    [Theory]
    [InlineData(DokPath.Confirmation, DokStage.Election, "Wybranie", "Bierzmowanie")]
    [InlineData(DokPath.BaptismCandidate, DokStage.Evangelization, "Ewangelizacja", "Kandydaci do Chrztu")]
    [InlineData(DokPath.Confirmation, DokStage.CloserFormation, "Formacja bliższa", "Bierzmowanie")]
    [InlineData(DokPath.Communion, DokStage.Neophyte, "Neofita", "Eucharystia")]
    public async Task CreateAsync_RejectsAStageOfAnotherPath_NamingBoth(DokPath path, DokStage stage, string stageLabel, string pathLabel)
    {
        var (db, service, personId, catechistId) = await SeedAsync();
        await using var _ = db;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Create(personId, catechistId, path, stage), default));

        Assert.Contains(stageLabel, ex.Message);
        Assert.Contains(pathLabel, ex.Message);
        Assert.Empty(db.DokCases);
    }

    [Fact]
    public async Task CreateAsync_RejectsAnUndefinedStageNumber()
    {
        var (db, service, personId, catechistId) = await SeedAsync();
        await using var _ = db;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Create(personId, catechistId, DokPath.Confirmation, (DokStage)0), default));
    }

    [Theory]
    [InlineData(DokPath.BaptismCandidate)]
    [InlineData(DokPath.Confirmation)]
    [InlineData(DokPath.Communion)]
    [InlineData(DokPath.Conversion)]
    [InlineData(DokPath.ReturnToUnity)]
    public async Task Graduate_IsAvailableOnEveryPath_AndMarksTheCaseAsCompleted(DokPath path)
    {
        var (db, service, personId, catechistId) = await SeedAsync();
        await using var _ = db;

        var created = await service.CreateAsync(Create(personId, catechistId, path, DokStage.Graduate), default);

        Assert.Equal(DokStage.Graduate, created.Stage);
        Assert.NotNull(created.CompletedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ChecksTheStageAgainstThePath_AlsoWhenOnlyThePathChanges()
    {
        var (db, service, personId, catechistId) = await SeedAsync();
        await using var _ = db;
        var created = await service.CreateAsync(Create(personId, catechistId, DokPath.BaptismCandidate, DokStage.Catechumenate), default);

        // ten sam etap, inna ścieżka: Katechumenat nie istnieje w Bierzmowaniu
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, new UpdateDokCaseRequest
        {
            PersonId = personId, Path = DokPath.Confirmation, Stage = DokStage.Catechumenate, CatechistPersonId = catechistId
        }, default));
        var moved = await service.UpdateAsync(created.Id, new UpdateDokCaseRequest
        {
            PersonId = personId, Path = DokPath.Confirmation, Stage = DokStage.Evangelization, CatechistPersonId = catechistId
        }, default);

        Assert.Equal((DokPath.Confirmation, DokStage.Evangelization), (moved!.Path, moved.Stage));
    }

    [Fact]
    public async Task Dashboard_ListsEveryStageInFormationOrder_WithTheirCounts()
    {
        var (db, _, personId, catechistId) = await SeedAsync();
        await using var _ = db;
        db.DokCases.AddRange(
            NewCase(personId, catechistId, DokPath.BaptismCandidate, DokStage.Catechumenate),
            NewCase(personId, catechistId, DokPath.BaptismCandidate, DokStage.Catechumenate),
            NewCase(personId, catechistId, DokPath.Confirmation, DokStage.Evangelization),
            NewCase(personId, catechistId, DokPath.Communion, DokStage.Graduate));
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db).GetSummaryAsync(default);

        Assert.Equal(
            new[] { "Evangelization", "CloserFormation", "Prekatechumenate", "Catechumenate", "Election", "Neophyte", "Graduate" },
            summary.DokCasesByStage.Select(s => s.Stage));
        Assert.Equal(new[] { 1, 0, 0, 2, 0, 0, 1 }, summary.DokCasesByStage.Select(s => s.Count));
    }

    private static DokCase NewCase(Guid personId, Guid catechistId, DokPath path, DokStage stage) => new()
    {
        Id = Guid.NewGuid(), PersonId = personId, CatechistPersonId = catechistId, Path = path, Stage = stage,
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };
}
