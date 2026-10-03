using DokPortal.Application.DokCases;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class AttendanceTests
{
    private sealed class StubScope : ICaseScopeProvider
    {
        private readonly CaseScope _scope;
        public StubScope(CaseScope scope) => _scope = scope;
        public Task<CaseScope> GetAsync(CancellationToken ct) => Task.FromResult(_scope);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Person NewPerson(string first, string last) => new()
    {
        Id = Guid.NewGuid(), FirstName = first, LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static DokCase NewCase(Person student, Person catechist) => new()
    {
        Id = Guid.NewGuid(), PersonId = student.Id, CatechistPersonId = catechist.Id, Path = DokPath.Confirmation,
        Stage = DokStage.Formation, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static Meeting NewMeeting(Guid caseId, int dayOffset, bool? attended, DateTime? deletedAtUtc = null) => new()
    {
        Id = Guid.NewGuid(), DokCaseId = caseId, MeetingDate = new DateOnly(2026, 10, 1).AddDays(dayOffset),
        IsAttended = attended, CreatedAtUtc = DateTime.UtcNow, DeletedAtUtc = deletedAtUtc
    };

    // --- SetAttendanceAsync ---

    [Fact]
    public async Task SetAttendanceAsync_SetsAndClearsTheAttendance()
    {
        await using var db = CreateContext();
        var student = NewPerson("Jan", "Kowalski");
        var catechist = NewPerson("Anna", "Maj");
        var dokCase = NewCase(student, catechist);
        var meeting = NewMeeting(dokCase.Id, 0, null);
        db.AddRange(student, catechist, dokCase, meeting);
        await db.SaveChangesAsync();
        var service = new MeetingService(db);

        var present = await service.SetAttendanceAsync(meeting.Id, true, default);
        var absent = await service.SetAttendanceAsync(meeting.Id, false, default);
        var cleared = await service.SetAttendanceAsync(meeting.Id, null, default);

        Assert.True(present!.IsAttended);
        Assert.False(absent!.IsAttended);
        Assert.Null(cleared!.IsAttended);
        Assert.Equal("Jan Kowalski", cleared.CaseLabel);
    }

    [Fact]
    public async Task SetAttendanceAsync_ReturnsNull_ForUnknownDeletedOrForeignMeetings()
    {
        await using var db = CreateContext();
        var student = NewPerson("Jan", "Kowalski");
        var owner = NewPerson("Anna", "Maj");
        var stranger = NewPerson("Beata", "Lis");
        var dokCase = NewCase(student, owner);
        var meeting = NewMeeting(dokCase.Id, 0, null);
        var deleted = NewMeeting(dokCase.Id, 1, null, DateTime.UtcNow);
        db.AddRange(student, owner, stranger, dokCase, meeting, deleted);
        await db.SaveChangesAsync();

        var asOwner = new MeetingService(db, new StubScope(new CaseScope(false, owner.Id)));
        var asStranger = new MeetingService(db, new StubScope(new CaseScope(false, stranger.Id)));

        Assert.Null(await asOwner.SetAttendanceAsync(Guid.NewGuid(), true, default));
        Assert.Null(await asOwner.SetAttendanceAsync(deleted.Id, true, default));
        Assert.Null(await asStranger.SetAttendanceAsync(meeting.Id, true, default));
        Assert.Null((await db.Meetings.AsNoTracking().FirstAsync(m => m.Id == meeting.Id)).IsAttended);
        Assert.True((await asOwner.SetAttendanceAsync(meeting.Id, true, default))!.IsAttended);
    }

    // --- frekwencja w DokCaseDto ---

    [Fact]
    public async Task DokCaseDto_CountsRecordedAndAttendedMeetings_IgnoringUnsetAndDeleted()
    {
        await using var db = CreateContext();
        var student = NewPerson("Jan", "Kowalski");
        var other = NewPerson("Piotr", "Nowak");
        var catechist = NewPerson("Anna", "Maj");
        var caseOne = NewCase(student, catechist);
        var caseTwo = NewCase(other, catechist);
        db.AddRange(student, other, catechist, caseOne, caseTwo,
            NewMeeting(caseOne.Id, 0, true), NewMeeting(caseOne.Id, 1, true), NewMeeting(caseOne.Id, 2, false),
            NewMeeting(caseOne.Id, 3, null), NewMeeting(caseOne.Id, 4, true, DateTime.UtcNow),
            NewMeeting(caseTwo.Id, 0, false));
        await db.SaveChangesAsync();
        var service = new DokCaseService(db);

        var page = await service.SearchAsync(null, 1, 20, default);
        var single = await service.GetByIdAsync(caseOne.Id, default);

        var one = page.Items.Single(i => i.Id == caseOne.Id);
        var two = page.Items.Single(i => i.Id == caseTwo.Id);
        Assert.Equal((3, 2), (one.MeetingsRecorded, one.MeetingsAttended));
        Assert.Equal((1, 0), (two.MeetingsRecorded, two.MeetingsAttended));
        Assert.Equal((3, 2), (single!.MeetingsRecorded, single.MeetingsAttended));
    }

    [Fact]
    public async Task DokCaseDto_WithoutMeetings_HasZeroCounts()
    {
        await using var db = CreateContext();
        var student = NewPerson("Jan", "Kowalski");
        var catechist = NewPerson("Anna", "Maj");
        var dokCase = NewCase(student, catechist);
        db.AddRange(student, catechist, dokCase);
        await db.SaveChangesAsync();

        var dto = await new DokCaseService(db).GetByIdAsync(dokCase.Id, default);

        Assert.Equal((0, 0), (dto!.MeetingsRecorded, dto.MeetingsAttended));
    }
}
