using DokPortal.Domain.Formation;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class DashboardServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Person NewPerson() => new()
    {
        Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski",
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static DokCase NewDokCase(DokStage stage, DateTime? deletedAtUtc = null) => new()
    {
        Id = Guid.NewGuid(), PersonId = Guid.NewGuid(), CatechistPersonId = Guid.NewGuid(),
        Path = DokPath.Confirmation, Stage = stage,
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, DeletedAtUtc = deletedAtUtc
    };

    private static CaseDocument NewDocument(Guid dokCaseId, bool isProvided) => new()
    {
        Id = Guid.NewGuid(), DokCaseId = dokCaseId, Name = "Metryka chrztu", IsProvided = isProvided,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static Meeting NewMeeting(DateOnly date, DateTime? deletedAtUtc = null) => new()
    {
        Id = Guid.NewGuid(), MeetingDate = date, CreatedAtUtc = DateTime.UtcNow, DeletedAtUtc = deletedAtUtc
    };

    [Fact]
    public async Task GetSummaryAsync_CountsPeopleAndParishes()
    {
        await using var db = CreateContext();
        db.Parishes.Add(new Parish { Id = Guid.NewGuid(), Name = "św. Pawła" });
        db.People.Add(new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski" });
        await db.SaveChangesAsync();

        var service = new DashboardService(db);
        var summary = await service.GetSummaryAsync(default);

        Assert.Equal(1, summary.PeopleCount);
        Assert.Equal(1, summary.ParishCount);
    }

    [Fact]
    public async Task GetSummaryAsync_GroupsActiveDokCasesByStage_InEnumOrder_IncludingEmptyStages()
    {
        await using var db = CreateContext();
        db.DokCases.AddRange(
            NewDokCase(DokStage.Formation),
            NewDokCase(DokStage.Formation),
            NewDokCase(DokStage.Graduate),
            NewDokCase(DokStage.Formation, deletedAtUtc: DateTime.UtcNow));
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db).GetSummaryAsync(default);

        Assert.Equal(
            new[] { "Application", "Formation", "Sacrament", "Graduate" },
            summary.DokCasesByStage.Select(s => s.Stage));
        Assert.Equal(new[] { 0, 2, 0, 1 }, summary.DokCasesByStage.Select(s => s.Count));
    }

    [Fact]
    public async Task GetSummaryAsync_CountsDistinctActiveCasesWithMissingDocuments()
    {
        await using var db = CreateContext();
        var caseWithTwoMissing = NewDokCase(DokStage.Formation);
        var caseWithOneMissing = NewDokCase(DokStage.Formation);
        var caseAllProvided = NewDokCase(DokStage.Formation);
        var deletedCaseWithMissing = NewDokCase(DokStage.Formation, deletedAtUtc: DateTime.UtcNow);
        db.DokCases.AddRange(caseWithTwoMissing, caseWithOneMissing, caseAllProvided, deletedCaseWithMissing);
        db.CaseDocuments.AddRange(
            NewDocument(caseWithTwoMissing.Id, false),
            NewDocument(caseWithTwoMissing.Id, false),
            NewDocument(caseWithOneMissing.Id, false),
            NewDocument(caseAllProvided.Id, true),
            NewDocument(deletedCaseWithMissing.Id, false));
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db).GetSummaryAsync(default);

        Assert.Equal(2, summary.MissingDocumentsCasesCount);
    }

    [Fact]
    public async Task GetSummaryAsync_CountsMeetingsFromTodayThroughNextSevenDays()
    {
        await using var db = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Meetings.AddRange(
            NewMeeting(today.AddDays(-1)),
            NewMeeting(today),
            NewMeeting(today.AddDays(7)),
            NewMeeting(today.AddDays(8)),
            NewMeeting(today.AddDays(3), deletedAtUtc: DateTime.UtcNow));
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db).GetSummaryAsync(default);

        Assert.Equal(2, summary.UpcomingMeetingsCount);
    }

    [Fact]
    public async Task GetSummaryAsync_CountsActiveCandidates()
    {
        await using var db = CreateContext();
        var person = NewPerson();
        db.People.Add(person);
        db.Candidates.AddRange(
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = FormationCalendar.StartYearFor(1, DateOnly.FromDateTime(DateTime.UtcNow)), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = FormationCalendar.StartYearFor(2, DateOnly.FromDateTime(DateTime.UtcNow)), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = FormationCalendar.StartYearFor(3, DateOnly.FromDateTime(DateTime.UtcNow)), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, DeletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db).GetSummaryAsync(default);

        Assert.Equal(2, summary.ActiveCandidatesCount);
    }

    [Fact]
    public async Task GetSummaryAsync_ActiveCandidates_ExcludesThoseWhoFinishedOrWereStopped()
    {
        await using var db = CreateContext();
        var person = NewPerson();
        db.People.Add(person);
        db.Candidates.AddRange(
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = 2026, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = 2024, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = 2023, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = 2026, IsFormationStopped = true, FormationStopNote = "x", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var before = await new DashboardService(db, null, new FixedTimeProvider(2027, 8, 31)).GetSummaryAsync(default);
        var after = await new DashboardService(db, null, new FixedTimeProvider(2027, 9, 1)).GetSummaryAsync(default);

        Assert.Equal(2, before.ActiveCandidatesCount);
        Assert.Equal(1, after.ActiveCandidatesCount);
    }
}
