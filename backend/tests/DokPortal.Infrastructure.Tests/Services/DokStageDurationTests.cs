using DokPortal.Application.DokCases;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Podopieczny, który jest na jednym etapie dłużej niż rok, trafia na pulpit jako sprawa wymagająca uwagi.</summary>
public class DokStageDurationTests
{
    private static readonly FixedTimeProvider Now = new(2026, 10, 7);

    private sealed class OwnCasesScope : ICaseScopeProvider
    {
        private readonly Guid _personId;
        public OwnCasesScope(Guid personId) => _personId = personId;
        public Task<CaseScope> GetAsync(CancellationToken ct) => Task.FromResult(new CaseScope(false, _personId));
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Person NewPerson(string last) =>
        new() { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };

    private static DokCase NewCase(Person person, Person catechist, DokStage stage, DateTime since, DokPath path = DokPath.BaptismCandidate) => new()
    {
        Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id, Path = path, Stage = stage,
        StageSinceUtc = since, CreatedAtUtc = since, UpdatedAtUtc = since
    };

    // ------------------------------------------------------------------ dashboard
    [Fact]
    public async Task Dashboard_ListsCasesOnTheSameStageForMoreThanAYear_OldestFirst()
    {
        await using var db = CreateContext();
        var catechist = NewPerson("Maj");
        var oldest = NewPerson("Najdluzszy");
        var older = NewPerson("Dluzszy");
        var fresh = NewPerson("Swiezy");
        db.People.AddRange(catechist, oldest, older, fresh);
        db.DokCases.AddRange(
            NewCase(older, catechist, DokStage.Catechumenate, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            NewCase(oldest, catechist, DokStage.Prekatechumenate, new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            NewCase(fresh, catechist, DokStage.Prekatechumenate, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db, null, Now).GetSummaryAsync(default);

        Assert.Equal(2, summary.StalledCasesCount);
        Assert.Equal(new[] { "Jan Najdluzszy", "Jan Dluzszy" }, summary.StalledCases.Select(c => c.PersonFullName));
        var first = summary.StalledCases[0];
        Assert.Equal("Prekatechumenate", first.Stage);
        Assert.Equal("BaptismCandidate", first.Path);
        Assert.Equal(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), first.StageSinceUtc);
        Assert.Equal(31, first.MonthsOnStage);
    }

    [Fact]
    public async Task Dashboard_ExactlyOneYearIsNotYetTooLong_OneDayMoreIs()
    {
        await using var db = CreateContext();
        var catechist = NewPerson("Maj");
        var exact = NewPerson("Rowny");
        var over = NewPerson("Ponad");
        db.People.AddRange(catechist, exact, over);
        var now = Now.GetUtcNow().UtcDateTime;
        db.DokCases.AddRange(
            NewCase(exact, catechist, DokStage.Catechumenate, now.AddYears(-1)),
            NewCase(over, catechist, DokStage.Catechumenate, now.AddYears(-1).AddDays(-1)));
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db, null, Now).GetSummaryAsync(default);

        Assert.Equal(new[] { "Jan Ponad" }, summary.StalledCases.Select(c => c.PersonFullName));
    }

    [Fact]
    public async Task Dashboard_AGraduateOrADeletedCase_IsNeverStalled()
    {
        await using var db = CreateContext();
        var catechist = NewPerson("Maj");
        var graduate = NewPerson("Absolwent");
        var deleted = NewPerson("Usuniety");
        db.People.AddRange(catechist, graduate, deleted);
        var long_ago = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var removed = NewCase(deleted, catechist, DokStage.Catechumenate, long_ago);
        removed.DeletedAtUtc = DateTime.UtcNow;
        db.DokCases.AddRange(NewCase(graduate, catechist, DokStage.Graduate, long_ago), removed);
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db, null, Now).GetSummaryAsync(default);

        Assert.Empty(summary.StalledCases);
        Assert.Equal(0, summary.StalledCasesCount);
    }

    [Fact]
    public async Task Dashboard_ShowsAtMostTwentyCases_ButCountsAll()
    {
        await using var db = CreateContext();
        var catechist = NewPerson("Maj");
        db.People.Add(catechist);
        for (var i = 0; i < 25; i++)
        {
            var person = NewPerson($"Osoba{i:00}");
            db.People.Add(person);
            db.DokCases.Add(NewCase(person, catechist, DokStage.Catechumenate, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i)));
        }
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db, null, Now).GetSummaryAsync(default);

        Assert.Equal(25, summary.StalledCasesCount);
        Assert.Equal(20, summary.StalledCases.Count);
        Assert.Equal("Jan Osoba00", summary.StalledCases[0].PersonFullName);
    }

    [Fact]
    public async Task Dashboard_ACatechistSeesOnlyTheirOwnStalledCases()
    {
        await using var db = CreateContext();
        var mine = NewPerson("Moja");
        var other = NewPerson("Obca");
        var myStudent = NewPerson("MojPodopieczny");
        var otherStudent = NewPerson("CudzyPodopieczny");
        db.People.AddRange(mine, other, myStudent, otherStudent);
        var long_ago = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.DokCases.AddRange(NewCase(myStudent, mine, DokStage.Catechumenate, long_ago), NewCase(otherStudent, other, DokStage.Catechumenate, long_ago));
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db, new OwnCasesScope(mine.Id), Now).GetSummaryAsync(default);

        Assert.Equal(new[] { "Jan MojPodopieczny" }, summary.StalledCases.Select(c => c.PersonFullName));
        Assert.Equal(1, summary.StalledCasesCount);
    }

    // ------------------------------------------------------------------ the date a case entered its stage
    private static async Task<(AppDbContext Db, Guid PersonId, Guid CatechistId)> SeedPeopleAsync()
    {
        var db = CreateContext();
        var person = NewPerson("Kowalski");
        var catechist = NewPerson("Maj");
        db.People.AddRange(person, catechist);
        await db.SaveChangesAsync();
        return (db, person.Id, catechist.Id);
    }

    [Fact]
    public async Task ANewCase_StartsItsStageToday()
    {
        var (db, personId, catechistId) = await SeedPeopleAsync();
        await using var _ = db;
        var service = new DokCaseService(db, null, Now);

        await service.CreateAsync(new CreateDokCaseRequest { PersonId = personId, Path = DokPath.Confirmation, Stage = DokStage.Evangelization, CatechistPersonId = catechistId }, default);

        Assert.Equal(Now.GetUtcNow().UtcDateTime, (await db.DokCases.SingleAsync()).StageSinceUtc);
    }

    [Fact]
    public async Task Update_ChangingTheStageRestartsTheClock_ButKeepingItDoesNot()
    {
        var (db, personId, catechistId) = await SeedPeopleAsync();
        await using var _ = db;
        var created = await new DokCaseService(db, null, new FixedTimeProvider(2025, 1, 10)).CreateAsync(new CreateDokCaseRequest
        {
            PersonId = personId, Path = DokPath.BaptismCandidate, Stage = DokStage.Prekatechumenate, CatechistPersonId = catechistId
        }, default);
        var later = new DokCaseService(db, null, new FixedTimeProvider(2026, 3, 5));

        await later.UpdateAsync(created.Id, new UpdateDokCaseRequest
        {
            PersonId = personId, Path = DokPath.BaptismCandidate, Stage = DokStage.Prekatechumenate, CatechistPersonId = catechistId
        }, default);
        var sameStage = (await db.DokCases.SingleAsync()).StageSinceUtc;
        await later.UpdateAsync(created.Id, new UpdateDokCaseRequest
        {
            PersonId = personId, Path = DokPath.BaptismCandidate, Stage = DokStage.Catechumenate, CatechistPersonId = catechistId
        }, default);
        var newStage = (await db.DokCases.SingleAsync()).StageSinceUtc;

        Assert.Equal(new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc), sameStage);
        Assert.Equal(new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc), newStage);
    }
}
