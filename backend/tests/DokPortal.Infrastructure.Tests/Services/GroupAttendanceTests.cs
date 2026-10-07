using DokPortal.Application.DokCases;
using DokPortal.Application.Meetings;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class GroupAttendanceTests
{
    private sealed class StubScope : ICaseScopeProvider
    {
        private readonly CaseScope _scope;
        public StubScope(CaseScope scope) => _scope = scope;
        public Task<CaseScope> GetAsync(CancellationToken ct) => Task.FromResult(_scope);
    }

    private sealed class World
    {
        public required AppDbContext Db { get; init; }
        public required Guid CatechistA { get; init; }
        public required Guid CatechistB { get; init; }
        public required Guid CaseA1 { get; init; }
        public required Guid CaseA2 { get; init; }
        public required Guid CaseB { get; init; }

        public MeetingService AsA => new(Db, new StubScope(new CaseScope(false, CatechistA)));
        public MeetingService AsB => new(Db, new StubScope(new CaseScope(false, CatechistB)));
        public MeetingService AsAll => new(Db, new StubScope(CaseScope.All));
    }

    private static readonly DateOnly Day = new(2026, 10, 10);

    private static Person NewPerson(string first, string last) => new()
    {
        Id = Guid.NewGuid(), FirstName = first, LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static DokCase NewCase(Person student, Person catechist) => new()
    {
        Id = Guid.NewGuid(), PersonId = student.Id, CatechistPersonId = catechist.Id, Path = DokPath.Confirmation,
        Stage = DokStage.Evangelization, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static async Task<World> SeedAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var catA = NewPerson("Anna", "Maj");
        var catB = NewPerson("Beata", "Lis");
        var s1 = NewPerson("Jan", "Kowalski");
        var s2 = NewPerson("Ewa", "Zielińska");
        var s3 = NewPerson("Piotr", "Nowak");
        var a1 = NewCase(s1, catA);
        var a2 = NewCase(s2, catA);
        var b = NewCase(s3, catB);
        db.AddRange(catA, catB, s1, s2, s3, a1, a2, b);
        await db.SaveChangesAsync();
        return new World { Db = db, CatechistA = catA.Id, CatechistB = catB.Id, CaseA1 = a1.Id, CaseA2 = a2.Id, CaseB = b.Id };
    }

    private static CreateMeetingRequest Group(string label, params (Guid CaseId, bool? Attended)[] attendees) => new()
    {
        GroupLabel = label, MeetingDate = Day,
        Attendees = attendees.Select(a => new AttendeeRequest { DokCaseId = a.CaseId, IsAttended = a.Attended }).ToList()
    };

    [Fact]
    public async Task Create_StoresGroupMeetingWithAttendees_AndOwnsItByTheCreator()
    {
        var w = await SeedAsync();

        var created = await w.AsA.CreateAsync(Group("Grupa wieczorna", (w.CaseA1, true), (w.CaseA2, false)), default);

        Assert.Equal(new[] { "Ewa Zielińska", "Jan Kowalski" }, created.Attendees.Select(a => a.PersonFullName).OrderBy(n => n));
        Assert.True(created.Attendees.Single(a => a.DokCaseId == w.CaseA1).IsAttended);
        Assert.False(created.Attendees.Single(a => a.DokCaseId == w.CaseA2).IsAttended);
        Assert.Equal(w.CatechistA, (await w.Db.Meetings.AsNoTracking().SingleAsync()).CatechistPersonId);
    }

    [Fact]
    public async Task Create_RejectsMeetingsThatAreBothIndividualAndGroup_AndDuplicateAttendees()
    {
        var w = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => w.AsAll.CreateAsync(new CreateMeetingRequest
        {
            DokCaseId = w.CaseA1, MeetingDate = Day,
            Attendees = new[] { new AttendeeRequest { DokCaseId = w.CaseA2 } }
        }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            w.AsAll.CreateAsync(Group("Grupa", (w.CaseA1, null), (w.CaseA1, true)), default));
        Assert.Empty(await w.Db.Meetings.ToListAsync());
    }

    [Fact]
    public async Task Create_RejectsAttendeesFromAnotherCatechistsCases()
    {
        var w = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            w.AsA.CreateAsync(Group("Grupa", (w.CaseA1, null), (w.CaseB, null)), default));

        var viewAll = await w.AsAll.CreateAsync(Group("Grupa mieszana", (w.CaseA1, null), (w.CaseB, null)), default);
        Assert.Equal(2, viewAll.Attendees.Count);
    }

    [Fact]
    public async Task GroupMeetings_AreVisibleToTheirOwnerAndViewAll_ButNotToOtherCatechists()
    {
        var w = await SeedAsync();
        var created = await w.AsA.CreateAsync(Group("Grupa A", (w.CaseA1, null)), default);

        Assert.Contains(await w.AsA.GetAllAsync(default), m => m.Id == created.Id);
        Assert.NotNull(await w.AsA.GetByIdAsync(created.Id, default));
        Assert.Contains(await w.AsAll.GetAllAsync(default), m => m.Id == created.Id);
        Assert.DoesNotContain(await w.AsB.GetAllAsync(default), m => m.Id == created.Id);
        Assert.Null(await w.AsB.GetByIdAsync(created.Id, default));
        Assert.False(await w.AsB.DeleteAsync(created.Id, "b", default));
    }

    [Fact]
    public async Task GroupMeetingWithoutAttendees_IsStillVisibleToItsCreator()
    {
        var w = await SeedAsync();

        var created = await w.AsA.CreateAsync(new CreateMeetingRequest { GroupLabel = "Spotkanie organizacyjne", MeetingDate = Day }, default);

        Assert.Contains(await w.AsA.GetAllAsync(default), m => m.Id == created.Id);
        Assert.Empty(created.Attendees);
    }

    [Fact]
    public async Task Update_ReplacesTheAttendeeList_UpdatingKeptRowsAndRemovingDroppedOnes()
    {
        var w = await SeedAsync();
        var created = await w.AsA.CreateAsync(Group("Grupa", (w.CaseA1, null), (w.CaseA2, true)), default);

        var updated = await w.AsA.UpdateAsync(created.Id, new CreateMeetingRequest
        {
            GroupLabel = "Grupa po zmianie", MeetingDate = Day, Notes = "uwagi",
            Attendees = new[] { new AttendeeRequest { DokCaseId = w.CaseA1, IsAttended = false } }
        }, default);

        Assert.Equal("Grupa po zmianie", updated!.GroupLabel);
        var attendee = Assert.Single(updated.Attendees);
        Assert.Equal(w.CaseA1, attendee.DokCaseId);
        Assert.False(attendee.IsAttended);
        Assert.Equal(1, await w.Db.MeetingAttendees.CountAsync());
    }

    [Fact]
    public async Task Update_ToAnIndividualMeeting_ClearsAttendeesAndOwner()
    {
        var w = await SeedAsync();
        var created = await w.AsA.CreateAsync(Group("Grupa", (w.CaseA1, null)), default);

        var updated = await w.AsA.UpdateAsync(created.Id, new CreateMeetingRequest { DokCaseId = w.CaseA2, MeetingDate = Day }, default);

        Assert.Empty(updated!.Attendees);
        Assert.Equal(w.CaseA2, updated.DokCaseId);
        Assert.Null((await w.Db.Meetings.AsNoTracking().SingleAsync()).CatechistPersonId);
        Assert.Empty(await w.Db.MeetingAttendees.ToListAsync());
    }

    [Fact]
    public async Task SetAttendeeAttendance_SetsAndClearsTheAttendeesState()
    {
        var w = await SeedAsync();
        var created = await w.AsA.CreateAsync(Group("Grupa", (w.CaseA1, null), (w.CaseA2, null)), default);

        var present = await w.AsA.SetAttendeeAttendanceAsync(created.Id, w.CaseA1, true, default);
        var cleared = await w.AsA.SetAttendeeAttendanceAsync(created.Id, w.CaseA1, null, default);

        Assert.True(present!.Attendees.Single(a => a.DokCaseId == w.CaseA1).IsAttended);
        Assert.Null(cleared!.Attendees.Single(a => a.DokCaseId == w.CaseA1).IsAttended);
        Assert.Null(cleared.Attendees.Single(a => a.DokCaseId == w.CaseA2).IsAttended);
    }

    [Fact]
    public async Task SetAttendeeAttendance_ReturnsNull_ForUnknownMeetingAttendeeOrForeignMeeting()
    {
        var w = await SeedAsync();
        var created = await w.AsA.CreateAsync(Group("Grupa", (w.CaseA1, null)), default);

        Assert.Null(await w.AsA.SetAttendeeAttendanceAsync(Guid.NewGuid(), w.CaseA1, true, default));
        Assert.Null(await w.AsA.SetAttendeeAttendanceAsync(created.Id, w.CaseA2, true, default));
        Assert.Null(await w.AsB.SetAttendeeAttendanceAsync(created.Id, w.CaseA1, true, default));
        Assert.Null((await w.Db.MeetingAttendees.AsNoTracking().SingleAsync()).IsAttended);
    }

    [Fact]
    public async Task Frequency_CountsGroupAttendanceRows_AndIgnoresDeletedMeetings()
    {
        var w = await SeedAsync();
        var kept = await w.AsA.CreateAsync(Group("Grupa 1", (w.CaseA1, true), (w.CaseA2, false)), default);
        await w.AsA.CreateAsync(Group("Grupa 2", (w.CaseA1, false)), default);
        var removed = await w.AsA.CreateAsync(Group("Grupa 3", (w.CaseA1, true)), default);
        await w.AsA.CreateAsync(Group("Grupa 4", (w.CaseA1, null)), default);
        await w.AsA.DeleteAsync(removed.Id, "a", default);
        var individual = await w.AsA.CreateAsync(new CreateMeetingRequest { DokCaseId = w.CaseA1, MeetingDate = Day, IsAttended = true }, default);

        var cases = new DokCaseService(w.Db);
        var one = await cases.GetByIdAsync(w.CaseA1, default);
        var two = await cases.GetByIdAsync(w.CaseA2, default);

        Assert.NotNull(kept);
        Assert.NotNull(individual);
        Assert.Equal((3, 2), (one!.MeetingsRecorded, one.MeetingsAttended));
        Assert.Equal((1, 0), (two!.MeetingsRecorded, two.MeetingsAttended));
    }
}
