using DokPortal.Application.Candidates;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Rok formacji kandydata zmienia się tylko ręcznie (przycisk albo edycja), a każda zmiana zostawia ślad z datą i autorem.</summary>
public class CandidateFormationTests
{
    private static readonly CandidateActor Alice = new("user-alice", "alice@example.org");
    private static readonly CandidateActor Bob = new("user-bob", "bob@example.org");

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Guid> SeedPersonAsync(AppDbContext db, string last = "Kowalski")
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person.Id;
    }

    private static CreateCandidateRequest Request(Guid personId, int year = 1, bool stopped = false, string? note = null, bool completed = false) => new()
    {
        PersonId = personId, Year = year, OpinionsCollected = 0, IsFormationStopped = stopped, FormationStopNote = note, IsFormationCompleted = completed
    };

    private static UpdateCandidateRequest Update(Guid personId, int year, bool stopped = false, string? note = null, bool completed = false) => new()
    {
        PersonId = personId, Year = year, OpinionsCollected = 0, IsFormationStopped = stopped, FormationStopNote = note, IsFormationCompleted = completed
    };

    [Fact]
    public async Task ANewCandidate_IsInTheChosenYearFromToday_WithAnEnrolmentEvent()
    {
        await using var db = CreateContext();
        var service = new CandidateService(db, new FixedTimeProvider(2026, 10, 7));

        var created = await service.CreateAsync(Request(await SeedPersonAsync(db), year: 2), default, Alice);

        Assert.Equal((2, "InFormation", false), (created.Year, created.Status, created.IsFormationCompleted));
        Assert.Equal(new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc), created.YearSinceUtc);
        var enrolled = Assert.Single(created.Events);
        Assert.Equal(("Enrolled", (int?)null, (int?)2, "alice@example.org"), (enrolled.Kind, enrolled.FromYear, enrolled.ToYear, enrolled.PerformedBy));
        Assert.Equal(created.YearSinceUtc, enrolled.AtUtc);
    }

    [Fact]
    public async Task TheYearDoesNotChangeWithTheCalendar_NotEvenAfterTheFirstOfSeptember()
    {
        await using var db = CreateContext();
        var created = await new CandidateService(db, new FixedTimeProvider(2026, 10, 7)).CreateAsync(Request(await SeedPersonAsync(db)), default);

        var years = new[] { (2027, 9, 1), (2029, 9, 1), (2035, 1, 1) }
            .Select(d => new CandidateService(db, new FixedTimeProvider(d.Item1, d.Item2, d.Item3)).GetByIdAsync(created.Id, default).Result!)
            .Select(c => (c.Year, c.Status));

        Assert.All(years, y => Assert.Equal((1, "InFormation"), y));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Create_And_Update_RejectAYearOutsideOneToThree(int year)
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);
        var personId = await SeedPersonAsync(db);
        var created = await service.CreateAsync(Request(personId), default);

        var onCreate = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request(personId, year), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, Update(personId, year), default));

        Assert.Contains("Rok formacji", onCreate.Message);
    }

    [Fact]
    public async Task Update_WithTheSameYear_KeepsTheTimestampAndAddsNoEvent()
    {
        await using var db = CreateContext();
        var personId = await SeedPersonAsync(db);
        var created = await new CandidateService(db, new FixedTimeProvider(2026, 10, 7)).CreateAsync(Request(personId), default, Alice);

        var saved = await new CandidateService(db, new FixedTimeProvider(2026, 12, 1)).UpdateAsync(created.Id, Update(personId, 1), default, Bob);

        Assert.Equal(created.YearSinceUtc, saved!.YearSinceUtc);
        Assert.Single(saved.Events);
    }

    [Fact]
    public async Task Update_ChangingTheYear_SetsTheTimestamp_AndRecordsWhoChangedItFromWhichToWhich()
    {
        await using var db = CreateContext();
        var personId = await SeedPersonAsync(db);
        var created = await new CandidateService(db, new FixedTimeProvider(2026, 10, 7)).CreateAsync(Request(personId), default, Alice);

        var saved = await new CandidateService(db, new FixedTimeProvider(2026, 12, 1)).UpdateAsync(created.Id, Update(personId, 3), default, Bob);

        Assert.Equal(3, saved!.Year);
        Assert.Equal(new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc), saved.YearSinceUtc);
        var latest = saved.Events[0];
        Assert.Equal(("Changed", (int?)1, (int?)3, "bob@example.org"), (latest.Kind, latest.FromYear, latest.ToYear, latest.PerformedBy));
        Assert.Equal(2, saved.Events.Count);
    }

    [Fact]
    public async Task Update_TickingCompleted_MovesToYearThreeCompleted_AndUntickingBringsHimBack()
    {
        await using var db = CreateContext();
        var personId = await SeedPersonAsync(db);
        var service = new CandidateService(db, new FixedTimeProvider(2026, 10, 7));
        var created = await service.CreateAsync(Request(personId, 2), default);

        var completed = await service.UpdateAsync(created.Id, Update(personId, 2, completed: true), default, Alice);
        var back = await service.UpdateAsync(created.Id, Update(personId, 3, completed: false), default, Alice);

        Assert.Equal((3, "Completed", true), (completed!.Year, completed.Status, completed.IsFormationCompleted));
        Assert.Equal("Completed", completed.Events[0].Kind);
        Assert.Equal(("InFormation", false), (back!.Status, back.IsFormationCompleted));
        Assert.Equal("Changed", back.Events[0].Kind);
    }

    [Fact]
    public async Task StoppingTheFormation_NeedsANote_ThenOverridesTheStatus_AndCanBeUndone()
    {
        await using var db = CreateContext();
        var personId = await SeedPersonAsync(db);
        var service = new CandidateService(db);
        var created = await service.CreateAsync(Request(personId), default);

        var noNote = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, Update(personId, 1, stopped: true, note: "   "), default));
        var stopped = await service.UpdateAsync(created.Id, Update(personId, 1, stopped: true, note: "  Zrezygnował po rozmowie  "), default);
        var resumed = await service.UpdateAsync(created.Id, Update(personId, 1), default);

        Assert.Contains("powód", noNote.Message);
        Assert.Equal(("Stopped", true, "Zrezygnował po rozmowie"), (stopped!.Status, stopped.IsFormationStopped, stopped.FormationStopNote));
        Assert.Equal(("InFormation", false, null), (resumed!.Status, resumed.IsFormationStopped, resumed.FormationStopNote));
    }

    [Fact]
    public async Task CreateAsync_CanStartAlreadyStopped_ButNeedsTheNoteToo()
    {
        await using var db = CreateContext();
        var personId = await SeedPersonAsync(db);
        var service = new CandidateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request(personId, stopped: true), default));
        var created = await service.CreateAsync(Request(personId, stopped: true, note: "Nie zakwalifikowany"), default);

        Assert.Equal("Stopped", created.Status);
    }

    [Fact]
    public async Task SearchAsync_ByYear_ReturnsOnlyThoseInThatYear_NotCompletedNorStopped()
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);
        await service.CreateAsync(Request(await SeedPersonAsync(db, "Pierwszy"), 1), default);
        await service.CreateAsync(Request(await SeedPersonAsync(db, "Trzeci"), 3), default);
        await service.CreateAsync(Request(await SeedPersonAsync(db, "Zatrzymany"), 3, true, "powód"), default);
        await service.CreateAsync(Request(await SeedPersonAsync(db, "Ukonczony"), 3, completed: true), default);

        var yearThree = await service.SearchAsync(3, 1, 20, default);
        var all = await service.SearchAsync(null, 1, 20, default);

        Assert.Equal("Jan Trzeci", yearThree.Items.Single().PersonFullName);
        Assert.Equal(1, yearThree.TotalCount);
        Assert.Equal(4, all.TotalCount);
    }

    [Fact]
    public async Task InFormation_AndCompleted_FilterByTheFlags_ExcludingStoppedOnes()
    {
        await using var db = CreateContext();
        var person = await SeedPersonAsync(db);
        db.Candidates.AddRange(
            NewCandidate(person, 1), NewCandidate(person, 3), NewCandidate(person, 3, completed: true),
            NewCandidate(person, 3, completed: true, stopped: true), NewCandidate(person, 2, stopped: true));
        await db.SaveChangesAsync();

        var inFormation = await db.Candidates.InFormation().Select(c => c.FormationYear).OrderBy(y => y).ToListAsync();
        var completed = await db.Candidates.Completed().CountAsync();

        Assert.Equal(new[] { 1, 3 }, inFormation);
        Assert.Equal(1, completed);
    }

    private static Candidate NewCandidate(Guid personId, int year, bool completed = false, bool stopped = false) => new()
    {
        Id = Guid.NewGuid(), PersonId = personId, FormationYear = year, IsFormationCompleted = completed, IsFormationStopped = stopped,
        FormationStopNote = stopped ? "x" : null, FormationYearSinceUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };
}
