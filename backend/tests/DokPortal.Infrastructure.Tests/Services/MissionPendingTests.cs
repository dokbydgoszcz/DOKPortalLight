using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Katechiści, którzy ukończyli formację, czekają na udzielenie posługi, a „Udziel posłania” zakłada im misję.</summary>
public class MissionPendingTests
{
    private static readonly FixedTimeProvider Time = new(2026, 10, 3);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Person NewPerson(string last, string? parishName = null)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        if (parishName is not null)
        {
            person.Parish = new Parish { Id = Guid.NewGuid(), Name = parishName };
            person.ParishId = person.Parish.Id;
        }
        return person;
    }

    /// <summary>Kandydat ze znacznikiem ukończenia formacji (domyślnie 1 września 2026) albo jeszcze w trakcie formacji.</summary>
    private static Candidate NewCandidate(Person person, bool completed, bool stopped = false, DateTime? since = null) => new()
    {
        Id = Guid.NewGuid(), PersonId = person.Id, FormationYear = completed ? 3 : 1, IsFormationCompleted = completed, IsFormationStopped = stopped,
        FormationStopNote = stopped ? "x" : null,
        FormationYearSinceUtc = since ?? new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static CanonicalMission NewMission(Person person, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = "Parafia", MissionStartDate = new DateOnly(2025, 9, 1),
        MissionEndDate = new DateOnly(2026, 9, 1), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
        DeletedAtUtc = deleted ? DateTime.UtcNow : null
    };

    [Fact]
    public async Task GetPendingAsync_ListsOnlyThoseWhoFinishedTheFormation_AndHaveNoMissionYet()
    {
        await using var db = CreateContext();
        var waiting = NewPerson("Czekajacy", "św. Jana");
        var inFormation = NewPerson("Uczacy");
        var stopped = NewPerson("Zatrzymany");
        var sent = NewPerson("Poslany");
        db.People.AddRange(waiting, inFormation, stopped, sent);
        db.Candidates.AddRange(
            NewCandidate(waiting, true), NewCandidate(inFormation, false), NewCandidate(stopped, true, stopped: true), NewCandidate(sent, true));
        db.CanonicalMissions.Add(NewMission(sent));
        await db.SaveChangesAsync();

        var pending = await new MissionService(db, Time).GetPendingAsync(default);

        var item = Assert.Single(pending);
        Assert.Equal(("Jan Czekajacy", "św. Jana", waiting.Id), (item.PersonFullName, item.ParishName, item.PersonId));
        Assert.Equal(new DateOnly(2026, 9, 1), item.FormationCompletedOn);
    }

    [Fact]
    public async Task GetPendingAsync_APersonWithTwoFinishedRecords_IsListedOnce_AndADeletedMissionBringsThemBack()
    {
        await using var db = CreateContext();
        var person = NewPerson("Kowalski");
        db.People.Add(person);
        db.Candidates.AddRange(NewCandidate(person, true, since: new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc)), NewCandidate(person, true));
        db.CanonicalMissions.Add(NewMission(person, deleted: true));
        await db.SaveChangesAsync();

        var pending = await new MissionService(db, Time).GetPendingAsync(default);

        Assert.Single(pending);
    }

    [Fact]
    public async Task GrantAsync_CreatesTheMissionWithTodaysDate_AndTheWaitingEntryDisappears()
    {
        await using var db = CreateContext();
        var person = NewPerson("Kowalski");
        db.People.Add(person);
        db.Candidates.Add(NewCandidate(person, true));
        await db.SaveChangesAsync();
        var service = new MissionService(db, Time);

        var mission = await service.GrantAsync(person.Id, default);

        Assert.Equal(person.Id, mission.PersonId);
        Assert.Equal(new DateOnly(2026, 10, 3), mission.GrantedDate);
        Assert.Equal(new DateOnly(2026, 10, 3), mission.MissionStartDate);
        Assert.Equal(new DateOnly(2027, 10, 3), mission.MissionEndDate);
        Assert.Equal("", mission.ServicePlace);
        Assert.False(mission.SentToDok);
        Assert.Empty(await service.GetPendingAsync(default));
    }

    [Fact]
    public async Task GrantAsync_RejectsSomeoneWhoDoesNotWait_AndWhoWasAlreadyGranted()
    {
        await using var db = CreateContext();
        var inFormation = NewPerson("Uczacy");
        var waiting = NewPerson("Czekajacy");
        db.People.AddRange(inFormation, waiting);
        db.Candidates.AddRange(NewCandidate(inFormation, false), NewCandidate(waiting, true));
        await db.SaveChangesAsync();
        var service = new MissionService(db, Time);
        await service.GrantAsync(waiting.Id, default);

        var notFinished = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantAsync(inFormation.Id, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantAsync(waiting.Id, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantAsync(Guid.NewGuid(), default));

        Assert.Contains("nie czeka", notFinished.Message);
        Assert.Single(await db.CanonicalMissions.ToListAsync());
    }

    [Fact]
    public async Task DeletingTheGrantedMission_PutsThePersonBackOnTheWaitingList()
    {
        await using var db = CreateContext();
        var person = NewPerson("Kowalski");
        db.People.Add(person);
        db.Candidates.Add(NewCandidate(person, true));
        await db.SaveChangesAsync();
        var service = new MissionService(db, Time);
        var mission = await service.GrantAsync(person.Id, default);

        await service.DeleteAsync(mission.Id, "u", default);

        Assert.Single(await service.GetPendingAsync(default));
    }

    [Fact]
    public async Task MissionStatus_UsesTheInjectedClock()
    {
        await using var db = CreateContext();
        var person = NewPerson("Kowalski");
        db.People.Add(person);
        db.CanonicalMissions.Add(new CanonicalMission
        {
            Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = "x", MissionStartDate = new DateOnly(2026, 1, 1),
            MissionEndDate = new DateOnly(2026, 10, 20), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var soon = await new MissionService(db, Time).SearchAsync(null, 1, 20, default);
        var expired = await new MissionService(db, new FixedTimeProvider(2026, 11, 1)).SearchAsync(null, 1, 20, default);
        var valid = await new MissionService(db, new FixedTimeProvider(2026, 6, 1)).SearchAsync(null, 1, 20, default);

        Assert.Equal("wygasa", soon.Items.Single().Status);
        Assert.Equal("wygasła", expired.Items.Single().Status);
        Assert.Equal("ważna", valid.Items.Single().Status);
    }

    [Fact]
    public async Task Missions_ListTheirAttachments()
    {
        await using var db = CreateContext();
        var person = NewPerson("Kowalski");
        db.People.Add(person);
        var mission = NewMission(person);
        db.CanonicalMissions.Add(mission);
        db.Attachments.Add(new Attachment
        {
            Id = Guid.NewGuid(), OwnerType = DokPortal.Domain.Enums.AttachmentOwnerType.Mission, OwnerId = mission.Id, FileName = "poslanie.pdf",
            ContentType = "application/pdf", SizeBytes = 10, BlobPath = "x", UploadedByUserId = "u", UploadedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new MissionService(db, Time);

        var page = await service.SearchAsync(null, 1, 20, default);
        var byId = await service.GetByIdAsync(mission.Id, default);

        Assert.Equal("poslanie.pdf", page.Items.Single().Attachments.Single().FileName);
        Assert.Equal("poslanie.pdf", byId!.Attachments.Single().FileName);
    }
}
