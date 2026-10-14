using DokPortal.Application.Candidates;
using DokPortal.Application.SkspClasses;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Warunki ukończenia SKŚP, ślad zatrzymania formacji i frekwencja z list obecności zajęć.</summary>
public class SkspCompletionAndClassesTests
{
    private static readonly CandidateActor Director = new("u1", "dyrektor@example.org");

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Candidate> SeedCandidateAsync(AppDbContext db, string last, int year = 3, bool stopped = false,
        bool allRetreats = false, int opinions = 0, int? attendance = null)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(), PersonId = person.Id, FormationYear = year, IsFormationStopped = stopped, FormationStopNote = stopped ? "choroba" : null,
            OpinionsCollected = opinions, AttendancePercentage = attendance, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        if (allRetreats)
            for (var y = 1; y <= 3; y++) candidate.Retreats.Add(new CandidateRetreat { Id = Guid.NewGuid(), Year = y, IsCompleted = true });
        db.People.Add(person);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();
        return candidate;
    }

    private static IReadOnlyList<CandidateRetreatDto> AllRetreats() =>
        Enumerable.Range(1, 3).Select(y => new CandidateRetreatDto { Year = y, IsCompleted = true }).ToList();

    [Fact]
    public void Gaps_ListMissingRetreatsOpinionsAndLowOrMissingAttendance()
    {
        var gaps = CandidateCompletion.Gaps(new[] { new CandidateRetreatDto { Year = 1, IsCompleted = true } }, 1, 2, 70);
        Assert.Equal(new[] { "rekolekcje niezaliczone: rok II, III", "opinie: 1 z 2", "frekwencja 70% (wymagane 80%)" }, gaps);
        Assert.Equal(new[] { "brak frekwencji" }, CandidateCompletion.Gaps(AllRetreats(), 2, 2, null));
        Assert.Empty(CandidateCompletion.Gaps(AllRetreats(), 2, 2, 80));
    }

    [Fact]
    public async Task CompletingWithGaps_IsRefusedUntilConfirmed_AndAllowedWhenConditionsAreMet()
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);
        var candidate = await SeedCandidateAsync(db, "Nowak");
        UpdateCandidateRequest Complete(bool confirm, bool ok = false) => new()
        {
            PersonId = candidate.PersonId, Year = 3, IsFormationCompleted = true, OpinionsCollected = ok ? 2 : 0,
            AttendancePercentage = ok ? 90 : 50, Retreats = ok ? AllRetreats() : Array.Empty<CandidateRetreatDto>(), ConfirmIncomplete = confirm
        };

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(candidate.Id, Complete(false), default));
        Assert.Contains("opinie: 0 z 2", refused.Message);
        Assert.Contains("frekwencja 50%", refused.Message);

        var confirmed = await service.UpdateAsync(candidate.Id, Complete(true), default);
        Assert.True(confirmed!.IsFormationCompleted);

        var other = await SeedCandidateAsync(db, "Lis");
        var fine = await service.UpdateAsync(other.Id, new UpdateCandidateRequest
        {
            PersonId = other.PersonId, Year = 3, IsFormationCompleted = true, OpinionsCollected = 2, AttendancePercentage = 85, Retreats = AllRetreats()
        }, default);
        Assert.True(fine!.IsFormationCompleted);
        Assert.Empty(fine.CompletionGaps);
    }

    [Fact]
    public async Task CreatingAnAlreadyCompletedCandidateWithGaps_NeedsConfirmation()
    {
        await using var db = CreateContext();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Ewa", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new CandidateService(db).CreateAsync(new CreateCandidateRequest
        {
            PersonId = person.Id, Year = 3, IsFormationCompleted = true, OpinionsCollected = 0
        }, default));
    }

    [Fact]
    public async Task AdvancingFromYearThree_SkipsCandidatesWithGaps_UnlessConfirmed()
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);
        var ready = await SeedCandidateAsync(db, "Gotowy", allRetreats: true, opinions: 2, attendance: 95);
        var missing = await SeedCandidateAsync(db, "Brakujacy");

        var result = await service.AdvanceAsync(new[] { ready.Id, missing.Id }, Director, default);

        Assert.Equal(1, result.Completed);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal(missing.Id, skipped.CandidateId);
        Assert.StartsWith("Braki do ukończenia: rekolekcje niezaliczone: rok I, II, III", skipped.Reason);

        var forced = await service.AdvanceAsync(new[] { missing.Id }, Director, default, confirmIncomplete: true);
        Assert.Equal(1, forced.Completed);
    }

    [Fact]
    public async Task StoppingAndResumingTheFormation_LeavesATrailWithTheReason()
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);
        var candidate = await SeedCandidateAsync(db, "Nowak", year: 2);
        UpdateCandidateRequest Req(bool stopped) => new()
        {
            PersonId = candidate.PersonId, Year = 2, OpinionsCollected = 0, IsFormationStopped = stopped, FormationStopNote = stopped ? " wyjazd za granicę " : null
        };

        await service.UpdateAsync(candidate.Id, Req(true), default, Director);
        var resumed = await service.UpdateAsync(candidate.Id, Req(false), default, Director);

        Assert.Equal(new[] { "Resumed", "Stopped" }, resumed!.Events.Take(2).Select(e => e.Kind));
        Assert.Equal("wyjazd za granicę", resumed.Events[1].Note);
        Assert.Equal("dyrektor@example.org", resumed.Events[0].PerformedBy);
    }

    [Fact]
    public async Task ANewClass_ListsTheCandidatesInFormationOnThatYear()
    {
        await using var db = CreateContext();
        var a = await SeedCandidateAsync(db, "Adamski", year: 2);
        var b = await SeedCandidateAsync(db, "Bielski", year: 2);
        await SeedCandidateAsync(db, "Inny", year: 1);
        await SeedCandidateAsync(db, "Zatrzymany", year: 2, stopped: true);
        var service = new SkspClassService(db);

        var created = await service.CreateAsync(new SaveSkspClassRequest
        {
            Year = 2, ClassDate = new DateOnly(2026, 10, 17), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(13, 0), Topic = "  Liturgia  "
        }, default);

        Assert.Equal("Liturgia", created.Topic);
        Assert.Equal(new[] { a.Id, b.Id }, created.Attendees.Select(x => x.CandidateId));
        Assert.Equal(new[] { "Jan Adamski", "Jan Bielski" }, created.Attendees.Select(x => x.PersonFullName));
        Assert.All(created.Attendees, x => Assert.Null(x.IsPresent));
        Assert.Single(await service.GetAllAsync(2, default));
        Assert.Empty(await service.GetAllAsync(1, default));
    }

    [Fact]
    public async Task Attendance_FromTheClasses_ReplacesTheManualPercentage()
    {
        await using var db = CreateContext();
        var candidate = await SeedCandidateAsync(db, "Nowak", year: 1, attendance: 40);
        var classes = new SkspClassService(db);
        var candidates = new CandidateService(db);
        async Task<SkspClassDto> NewClass(int day) =>
            await classes.CreateAsync(new SaveSkspClassRequest { Year = 1, ClassDate = new DateOnly(2026, 10, day), Topic = "Zajęcia" }, default);

        Assert.Equal(40, (await candidates.GetByIdAsync(candidate.Id, default))!.AttendancePercentage);

        var c1 = await NewClass(3);
        var c2 = await NewClass(10);
        var c3 = await NewClass(17);
        await NewClass(24); // obecność jeszcze nie zaznaczona – nie liczy się
        await classes.SetAttendanceAsync(c1.Id, new[] { new SkspAttendanceItem { CandidateId = candidate.Id, IsPresent = true } }, default);
        await classes.SetAttendanceAsync(c2.Id, new[] { new SkspAttendanceItem { CandidateId = candidate.Id, IsPresent = true } }, default);
        await classes.SetAttendanceAsync(c3.Id, new[] { new SkspAttendanceItem { CandidateId = candidate.Id, IsPresent = false } }, default);

        var dto = (await candidates.SearchAsync(null, 1, 20, default)).Items.Single();
        Assert.Equal((67, true, 2, 3), (dto.AttendancePercentage, dto.AttendanceFromClasses, dto.ClassesAttended, dto.ClassesRecorded));

        await classes.DeleteAsync(c3.Id, "admin", default);
        Assert.Equal(100, (await candidates.GetByIdAsync(candidate.Id, default))!.AttendancePercentage);
    }

    [Fact]
    public async Task Attendance_OnlyForPeopleOnTheList_AndLateJoinersCanBeAdded()
    {
        await using var db = CreateContext();
        await SeedCandidateAsync(db, "Pierwszy", year: 1);
        var service = new SkspClassService(db);
        var created = await service.CreateAsync(new SaveSkspClassRequest { Year = 1, ClassDate = new DateOnly(2026, 10, 3), Topic = "Start" }, default);
        var late = await SeedCandidateAsync(db, "Spozniony", year: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetAttendanceAsync(created.Id, new[] { new SkspAttendanceItem { CandidateId = late.Id, IsPresent = true } }, default));

        var refreshed = await service.RefreshAttendeesAsync(created.Id, default);
        Assert.Equal(2, refreshed!.Attendees.Count);
        Assert.Equal(2, (await service.RefreshAttendeesAsync(created.Id, default))!.Attendees.Count);
    }

    [Fact]
    public async Task Edits_ChangeTheDetailsButNotTheYear_AndInvalidInputIsRefused()
    {
        await using var db = CreateContext();
        var service = new SkspClassService(db);
        var created = await service.CreateAsync(new SaveSkspClassRequest { Year = 1, ClassDate = new DateOnly(2026, 10, 3), Topic = "Start" }, default);

        var updated = await service.UpdateAsync(created.Id, new SaveSkspClassRequest
        {
            Year = 1, ClassDate = new DateOnly(2026, 10, 4), StartTime = new TimeOnly(10, 0), Topic = "Nowy temat", Notes = " sala 3 "
        }, default);
        Assert.Equal(("Nowy temat", "sala 3", new DateOnly(2026, 10, 4)), (updated!.Topic, updated.Notes, updated.ClassDate));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, new SaveSkspClassRequest { Year = 2, ClassDate = new DateOnly(2026, 10, 4), Topic = "x" }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new SaveSkspClassRequest { Year = 4, ClassDate = new DateOnly(2026, 10, 4), Topic = "x" }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new SaveSkspClassRequest { Year = 1, ClassDate = new DateOnly(2026, 10, 4), Topic = " " }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new SaveSkspClassRequest { Year = 1, ClassDate = new DateOnly(2026, 10, 4), Topic = "x", EndTime = new TimeOnly(9, 0) }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new SaveSkspClassRequest { Year = 1, ClassDate = new DateOnly(2026, 10, 4), Topic = "x", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 0) }, default));

        Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new SaveSkspClassRequest { Year = 1, ClassDate = new DateOnly(2026, 10, 4), Topic = "x" }, default));
        Assert.Null(await service.SetAttendanceAsync(Guid.NewGuid(), Array.Empty<SkspAttendanceItem>(), default));
        Assert.Null(await service.RefreshAttendeesAsync(Guid.NewGuid(), default));
        Assert.False(await service.DeleteAsync(Guid.NewGuid(), "admin", default));
    }
}
