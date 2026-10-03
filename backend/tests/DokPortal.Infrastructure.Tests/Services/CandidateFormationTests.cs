using DokPortal.Application.Candidates;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Rok formacji awansuje sam od 1 września, po III roku kandydat jest „Completed”, a formację można zatrzymać ręcznie.</summary>
public class CandidateFormationTests
{
    private sealed class FixedTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTime(int y, int m, int d) => _now = new DateTimeOffset(y, m, d, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Guid> SeedPersonAsync(AppDbContext db, string last = "Kowalski")
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person.Id;
    }

    private static CreateCandidateRequest Request(Guid personId, int year = 1, bool stopped = false, string? note = null) => new()
    {
        PersonId = personId, Year = year, OpinionsCollected = 0, IsFormationStopped = stopped, FormationStopNote = note
    };

    [Fact]
    public async Task ACandidateSavedInYearOne_MovesToTheNextYearOnEveryFirstOfSeptember_AndFinishesAfterYearThree()
    {
        await using var db = CreateContext();
        var created = await new CandidateService(db, new FixedTime(2026, 10, 3)).CreateAsync(Request(await SeedPersonAsync(db)), default);
        Assert.Equal((1, "InFormation"), (created.Year, created.Status));

        (int, string) At(int y, int m, int d)
        {
            var dto = new CandidateService(db, new FixedTime(y, m, d)).GetByIdAsync(created.Id, default).Result!;
            return (dto.Year, dto.Status);
        }

        Assert.Equal((1, "InFormation"), At(2027, 8, 31));
        Assert.Equal((2, "InFormation"), At(2027, 9, 1));
        Assert.Equal((3, "InFormation"), At(2028, 9, 1));
        Assert.Equal((3, "InFormation"), At(2029, 8, 31));
        Assert.Equal((3, "Completed"), At(2029, 9, 1));
        Assert.Equal((3, "Completed"), At(2035, 1, 1));
    }

    [Fact]
    public async Task ACandidateSavedInYearTwo_FinishesAfterOnlyOneMoreYear()
    {
        await using var db = CreateContext();
        var created = await new CandidateService(db, new FixedTime(2026, 10, 3)).CreateAsync(Request(await SeedPersonAsync(db), year: 2), default);

        var next = await new CandidateService(db, new FixedTime(2027, 9, 1)).GetByIdAsync(created.Id, default);
        var after = await new CandidateService(db, new FixedTime(2028, 9, 1)).GetByIdAsync(created.Id, default);

        Assert.Equal((3, "InFormation"), (next!.Year, next.Status));
        Assert.Equal("Completed", after!.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Create_And_Update_RejectAYearOutsideOneToThree(int year)
    {
        await using var db = CreateContext();
        var service = new CandidateService(db, new FixedTime(2026, 10, 3));
        var personId = await SeedPersonAsync(db);
        var created = await service.CreateAsync(Request(personId), default);

        var onCreate = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request(personId, year), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, new UpdateCandidateRequest { PersonId = personId, Year = year, OpinionsCollected = 0 }, default));

        Assert.Contains("Rok formacji", onCreate.Message);
    }

    [Fact]
    public async Task EditingACandidateWithoutChangingTheYear_KeepsTheCalendar_EvenAfterFormationEnded()
    {
        await using var db = CreateContext();
        var personId = await SeedPersonAsync(db);
        var created = await new CandidateService(db, new FixedTime(2026, 10, 3)).CreateAsync(Request(personId), default);
        var later = new CandidateService(db, new FixedTime(2030, 1, 1));

        var shown = (await later.GetByIdAsync(created.Id, default))!;
        var saved = await later.UpdateAsync(created.Id, new UpdateCandidateRequest { PersonId = personId, Year = shown.Year, OpinionsCollected = 2 }, default);

        Assert.Equal("Completed", saved!.Status);
        Assert.Equal(2, saved.OpinionsCollected);
    }

    [Fact]
    public async Task ChangingTheYear_RecalculatesTheStartSoThatTheCandidateIsInThatYearNow()
    {
        await using var db = CreateContext();
        var personId = await SeedPersonAsync(db);
        var service = new CandidateService(db, new FixedTime(2026, 10, 3));
        var created = await service.CreateAsync(Request(personId), default);

        var updated = await service.UpdateAsync(created.Id, new UpdateCandidateRequest { PersonId = personId, Year = 3, OpinionsCollected = 0 }, default);

        Assert.Equal(3, updated!.Year);
        Assert.Equal(2024, (await db.Candidates.SingleAsync()).FormationStartYear);
    }

    [Fact]
    public async Task StoppingTheFormation_NeedsANote_ThenOverridesTheStatus_AndCanBeUndone()
    {
        await using var db = CreateContext();
        var personId = await SeedPersonAsync(db);
        var service = new CandidateService(db, new FixedTime(2026, 10, 3));
        var created = await service.CreateAsync(Request(personId), default);

        var noNote = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(created.Id, new UpdateCandidateRequest { PersonId = personId, Year = 1, OpinionsCollected = 0, IsFormationStopped = true, FormationStopNote = "   " }, default));
        var stopped = await service.UpdateAsync(created.Id, new UpdateCandidateRequest
        {
            PersonId = personId, Year = 1, OpinionsCollected = 0, IsFormationStopped = true, FormationStopNote = "  Zrezygnował po rozmowie  "
        }, default);
        var resumed = await service.UpdateAsync(created.Id, new UpdateCandidateRequest { PersonId = personId, Year = 1, OpinionsCollected = 0 }, default);

        Assert.Contains("powód", noNote.Message);
        Assert.Equal(("Stopped", true, "Zrezygnował po rozmowie"), (stopped!.Status, stopped.IsFormationStopped, stopped.FormationStopNote));
        Assert.Equal(("InFormation", false, null), (resumed!.Status, resumed.IsFormationStopped, resumed.FormationStopNote));
    }

    [Fact]
    public async Task CreateAsync_CanStartAlreadyStopped_ButNeedsTheNoteToo()
    {
        await using var db = CreateContext();
        var personId = await SeedPersonAsync(db);
        var service = new CandidateService(db, new FixedTime(2026, 10, 3));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request(personId, stopped: true), default));
        var created = await service.CreateAsync(Request(personId, stopped: true, note: "Nie zakwalifikowany"), default);

        Assert.Equal("Stopped", created.Status);
    }

    [Fact]
    public async Task SearchAsync_ByYear_ReturnsOnlyThoseCurrentlyInThatYear_NotCompletedNorStopped()
    {
        await using var db = CreateContext();
        var time = new FixedTime(2026, 10, 3);
        var service = new CandidateService(db, time);
        await service.CreateAsync(Request(await SeedPersonAsync(db, "Pierwszy"), 1), default);
        await service.CreateAsync(Request(await SeedPersonAsync(db, "Trzeci"), 3), default);
        await service.CreateAsync(Request(await SeedPersonAsync(db, "Zatrzymany"), 3, true, "powód"), default);
        db.Candidates.Add(new Candidate
        {
            Id = Guid.NewGuid(), PersonId = await SeedPersonAsync(db, "Ukonczony"), FormationStartYear = 2020,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var yearThree = await service.SearchAsync(3, 1, 20, default);
        var all = await service.SearchAsync(null, 1, 20, default);

        Assert.Equal("Jan Trzeci", yearThree.Items.Single().PersonFullName);
        Assert.Equal(1, yearThree.TotalCount);
        Assert.Equal(4, all.TotalCount);
    }

    [Fact]
    public async Task InFormation_AndCompleted_FilterByTheCalendar_ExcludingStoppedOnes()
    {
        await using var db = CreateContext();
        var person = await SeedPersonAsync(db);
        db.Candidates.AddRange(
            NewCandidate(person, 2026),
            NewCandidate(person, 2024),
            NewCandidate(person, 2023),
            NewCandidate(person, 2023, stopped: true),
            NewCandidate(person, 2022));
        await db.SaveChangesAsync();
        var today = new DateOnly(2026, 10, 3);

        var inFormation = await db.Candidates.InFormation(today).Select(c => c.FormationStartYear).OrderBy(y => y).ToListAsync();
        var completed = await db.Candidates.Completed(today).Select(c => c.FormationStartYear).OrderBy(y => y).ToListAsync();

        Assert.Equal(new[] { 2024, 2026 }, inFormation);
        Assert.Equal(new[] { 2022, 2023 }, completed);
    }

    private static Candidate NewCandidate(Guid personId, int startYear, bool stopped = false) => new()
    {
        Id = Guid.NewGuid(), PersonId = personId, FormationStartYear = startYear, IsFormationStopped = stopped,
        FormationStopNote = stopped ? "x" : null, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };
}
