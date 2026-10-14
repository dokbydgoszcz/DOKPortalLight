using DokPortal.Application.DokCases;
using DokPortal.Application.Meetings;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Zajęcia grupowe prowadzone przez kogoś innego są „udostępnione” katechiście, którego podopieczny w nich uczestniczy.</summary>
public class GroupMeetingSharingTests
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
    public async Task ACatechist_SeesSomeoneElsesGroupMeeting_WhenTheirStudentTakesPart_AndMarksOnlyTheirStudent()
    {
        var w = await SeedAsync();
        var created = await w.AsAll.CreateAsync(new CreateMeetingRequest
        {
            GroupLabel = "Rekolekcje", MeetingDate = Day, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(20, 0),
            Attendees = new[] { new AttendeeRequest { DokCaseId = w.CaseA1 }, new AttendeeRequest { DokCaseId = w.CaseB } }
        }, default);

        Assert.Contains(await w.AsA.GetAllAsync(default), m => m.Id == created.Id);
        Assert.Contains(await w.AsB.GetAllAsync(default), m => m.Id == created.Id);
        var marked = await w.AsA.SetAttendeeAttendanceAsync(created.Id, w.CaseA1, true, default);
        Assert.True(marked!.Attendees.Single(a => a.DokCaseId == w.CaseA1).IsAttended);
        Assert.Null(await w.AsA.SetAttendeeAttendanceAsync(created.Id, w.CaseB, true, default));
        Assert.Equal(new TimeOnly(18, 0), marked.StartTime);
    }

    [Fact]
    public async Task OnlyTheOrganiser_OrSomeoneSeeingAll_CanChangeOrDeleteAGroupMeeting()
    {
        var w = await SeedAsync();
        var created = await w.AsAll.CreateAsync(new CreateMeetingRequest
        {
            GroupLabel = "Rekolekcje", MeetingDate = Day, Attendees = new[] { new AttendeeRequest { DokCaseId = w.CaseA1 } }
        }, default);

        var edit = await Assert.ThrowsAsync<InvalidOperationException>(() => w.AsA.UpdateAsync(created.Id, new CreateMeetingRequest
        {
            GroupLabel = "Zmiana", MeetingDate = Day, Attendees = new[] { new AttendeeRequest { DokCaseId = w.CaseA1 } }
        }, default));
        var delete = await Assert.ThrowsAsync<InvalidOperationException>(() => w.AsA.DeleteAsync(created.Id, "a", default));

        Assert.Contains("prowadzi", edit.Message);
        Assert.Contains("prowadzi", delete.Message);
        Assert.True(await w.AsAll.DeleteAsync(created.Id, "admin", default));
    }

    [Fact]
    public async Task Times_AreKept_AndAnEndBeforeTheStartIsRefused()
    {
        var w = await SeedAsync();
        var bad = await Assert.ThrowsAsync<InvalidOperationException>(() => w.AsA.CreateAsync(new CreateMeetingRequest
        {
            DokCaseId = w.CaseA1, MeetingDate = Day, StartTime = new TimeOnly(20, 0), EndTime = new TimeOnly(18, 0)
        }, default));
        var noStart = await Assert.ThrowsAsync<InvalidOperationException>(() => w.AsA.CreateAsync(new CreateMeetingRequest
        {
            DokCaseId = w.CaseA1, MeetingDate = Day, EndTime = new TimeOnly(18, 0)
        }, default));

        Assert.Contains("późniejsza", bad.Message);
        Assert.Contains("rozpoczęcia", noStart.Message);
    }
}
