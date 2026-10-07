using DokPortal.Application.Candidates;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>„Przenieś do następnego roku” – jednego lub wielu zaznaczonych kandydatów, ze śladem kto i kiedy to zrobił.</summary>
public class CandidateAdvanceTests
{
    private static readonly CandidateActor Director = new("user-dir", "dyrektor@example.org");

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Candidate Add(AppDbContext db, string last, int year, bool completed = false, bool stopped = false)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(), PersonId = person.Id, FormationYear = year, IsFormationCompleted = completed, IsFormationStopped = stopped,
            FormationStopNote = stopped ? "x" : null, FormationYearSinceUtc = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        db.Candidates.Add(candidate);
        return candidate;
    }

    [Fact]
    public async Task AdvanceAsync_MovesEachCandidateOneYearUp_AndStampsTheMoment()
    {
        await using var db = CreateContext();
        var first = Add(db, "Pierwszy", 1);
        var second = Add(db, "Drugi", 2);
        await db.SaveChangesAsync();
        var service = new CandidateService(db, new FixedTimeProvider(2027, 9, 1));

        var result = await service.AdvanceAsync(new[] { first.Id, second.Id }, Director, default);

        Assert.Equal((2, 0, 0), (result.Advanced, result.Completed, result.Skipped.Count));
        var one = (await service.GetByIdAsync(first.Id, default))!;
        var two = (await service.GetByIdAsync(second.Id, default))!;
        Assert.Equal((2, "InFormation"), (one.Year, one.Status));
        Assert.Equal(3, two.Year);
        var stamp = new DateTime(2027, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(stamp, one.YearSinceUtc);
        var evt = one.Events[0];
        Assert.Equal(("Advanced", (int?)1, (int?)2, "dyrektor@example.org", stamp), (evt.Kind, evt.FromYear, evt.ToYear, evt.PerformedBy, evt.AtUtc));
    }

    [Fact]
    public async Task AdvanceAsync_FromYearThree_CompletesTheFormation()
    {
        await using var db = CreateContext();
        var third = Add(db, "Trzeci", 3);
        await db.SaveChangesAsync();
        var service = new CandidateService(db, new FixedTimeProvider(2027, 9, 1));

        var result = await service.AdvanceAsync(new[] { third.Id }, Director, default);

        Assert.Equal((0, 1), (result.Advanced, result.Completed));
        var dto = (await service.GetByIdAsync(third.Id, default))!;
        Assert.Equal(("Completed", 3, true), (dto.Status, dto.Year, dto.IsFormationCompleted));
        Assert.Equal(("Completed", (int?)3, (int?)null), (dto.Events[0].Kind, dto.Events[0].FromYear, dto.Events[0].ToYear));
    }

    [Fact]
    public async Task AdvanceAsync_SkipsStoppedCompletedAndUnknown_ExplainingWhy_AndMovesTheRest()
    {
        await using var db = CreateContext();
        var ok = Add(db, "Dobry", 1);
        var stopped = Add(db, "Zatrzymany", 1, stopped: true);
        var done = Add(db, "Ukonczony", 3, completed: true);
        await db.SaveChangesAsync();
        var service = new CandidateService(db, new FixedTimeProvider(2027, 9, 1));
        var unknown = Guid.NewGuid();

        var result = await service.AdvanceAsync(new[] { ok.Id, stopped.Id, done.Id, unknown }, Director, default);

        Assert.Equal((1, 0), (result.Advanced, result.Completed));
        Assert.Equal(3, result.Skipped.Count);
        Assert.Contains(result.Skipped, s => s.CandidateId == stopped.Id && s.Reason.Contains("zatrzymana"));
        Assert.Contains(result.Skipped, s => s.CandidateId == done.Id && s.Reason.Contains("ukończył"));
        Assert.Contains(result.Skipped, s => s.CandidateId == unknown && s.Reason.Contains("Nie znaleziono"));
        Assert.Equal(1, (await db.Candidates.AsNoTracking().SingleAsync(c => c.Id == stopped.Id)).FormationYear);
        Assert.Empty(db.CandidateFormationEvents.Where(e => e.CandidateId == stopped.Id));
    }

    [Fact]
    public async Task AdvanceAsync_ACandidateListedTwice_IsMovedOnlyOnce()
    {
        await using var db = CreateContext();
        var candidate = Add(db, "Pierwszy", 1);
        await db.SaveChangesAsync();
        var service = new CandidateService(db);

        var result = await service.AdvanceAsync(new[] { candidate.Id, candidate.Id }, Director, default);

        Assert.Equal(1, result.Advanced);
        Assert.Equal(2, (await db.Candidates.AsNoTracking().SingleAsync()).FormationYear);
    }

    [Fact]
    public async Task AdvanceAsync_NeedsAtLeastOneCandidate_AndNotAbsurdlyMany()
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);

        var empty = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AdvanceAsync(Array.Empty<Guid>(), Director, default));
        var tooMany = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AdvanceAsync(Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToList(), Director, default));

        Assert.Contains("co najmniej jednego", empty.Message);
        Assert.Contains("500", tooMany.Message);
    }

    [Fact]
    public async Task ACompletedCandidate_AppearsAmongThoseWaitingForTheMission_WithTheCompletionDate()
    {
        await using var db = CreateContext();
        var third = Add(db, "Trzeci", 3);
        await db.SaveChangesAsync();
        var time = new FixedTimeProvider(2027, 9, 1);
        await new CandidateService(db, time).AdvanceAsync(new[] { third.Id }, Director, default);

        var pending = await new MissionService(db, time).GetPendingAsync(default);

        var item = Assert.Single(pending);
        Assert.Equal(new DateOnly(2027, 9, 1), item.FormationCompletedOn);
    }

    [Fact]
    public async Task Events_AreListedNewestFirst_AndGoWithTheCandidateWhenItIsDeleted()
    {
        await using var db = CreateContext();
        var candidate = Add(db, "Pierwszy", 1);
        await db.SaveChangesAsync();
        var first = new CandidateService(db, new FixedTimeProvider(2026, 10, 1));
        await first.AdvanceAsync(new[] { candidate.Id }, Director, default);
        await new CandidateService(db, new FixedTimeProvider(2027, 9, 1)).AdvanceAsync(new[] { candidate.Id }, Director, default);

        var dto = (await first.GetByIdAsync(candidate.Id, default))!;

        Assert.Equal(new[] { 3, 2 }, dto.Events.Select(e => e.ToYear!.Value));
        Assert.True(dto.Events[0].AtUtc > dto.Events[1].AtUtc);
    }
}
