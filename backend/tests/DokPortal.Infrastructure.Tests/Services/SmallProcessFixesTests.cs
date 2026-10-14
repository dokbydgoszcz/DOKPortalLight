using DokPortal.Application.Candidates;
using DokPortal.Application.Missions;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Drobne luki z przeglądu procesu (C5, C8, C9).</summary>
public class SmallProcessFixesTests
{
    private static readonly FixedTimeProvider Time = new(2026, 10, 12);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Person> AddPersonAsync(AppDbContext db, string last)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person;
    }

    private static CreateCandidateRequest Candidate(Guid personId, bool completed = false) => new()
    {
        PersonId = personId, Year = 3, IsFormationCompleted = completed, OpinionsCollected = 0, ConfirmIncomplete = true
    };

    [Fact]
    public async Task APersonCanHaveOnlyOneCandidacy()
    {
        await using var db = CreateContext();
        var jan = await AddPersonAsync(db, "Kowalski");
        var ewa = await AddPersonAsync(db, "Nowak");
        var service = new CandidateService(db, Time);
        await service.CreateAsync(Candidate(jan.Id), default);
        var ewasCandidacy = await service.CreateAsync(Candidate(ewa.Id), default);

        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Candidate(jan.Id), default));
        var moved = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(ewasCandidacy.Id, new UpdateCandidateRequest
        {
            PersonId = jan.Id, Year = 3, OpinionsCollected = 0
        }, default));

        Assert.Contains("ma już kandydaturę", duplicate.Message);
        Assert.Contains("ma już kandydaturę", moved.Message);
    }

    [Fact]
    public async Task CompletingTheFormationCannotBeUndone_OnceTheCatechistWasSent()
    {
        await using var db = CreateContext();
        var jan = await AddPersonAsync(db, "Kowalski");
        var service = new CandidateService(db, Time);
        var candidacy = await service.CreateAsync(Candidate(jan.Id, completed: true), default);
        db.CanonicalMissions.Add(new CanonicalMission
        {
            Id = Guid.NewGuid(), PersonId = jan.Id, ServicePlace = "", MissionStartDate = new DateOnly(2026, 10, 1),
            MissionEndDate = new DateOnly(2027, 10, 1), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var undo = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(candidacy.Id, new UpdateCandidateRequest
        {
            PersonId = jan.Id, Year = 3, IsFormationCompleted = false, OpinionsCollected = 0
        }, default));

        Assert.Contains("posłania", undo.Message);
    }

    [Fact]
    public async Task AMissionCannotEndBeforeItStarts()
    {
        await using var db = CreateContext();
        var jan = await AddPersonAsync(db, "Kowalski");
        var service = new MissionService(db, Time);
        CreateMissionRequest Request(DateOnly start, DateOnly end) => new()
        {
            PersonId = jan.Id, ServicePlace = "Fara", MissionStartDate = start, MissionEndDate = end
        };

        var create = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request(new DateOnly(2026, 10, 1), new DateOnly(2026, 9, 1)), default));
        var ok = await service.CreateAsync(Request(new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1)), default);
        var update = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(ok.Id, new UpdateMissionRequest
        {
            PersonId = jan.Id, ServicePlace = "Fara", MissionStartDate = new DateOnly(2026, 10, 1), MissionEndDate = new DateOnly(2026, 1, 1)
        }, default));

        Assert.Contains("Koniec misji", create.Message);
        Assert.Contains("Koniec misji", update.Message);
    }

    [Fact]
    public async Task TheLastMeetingDate_ComesFromPastIndividualAndGroupMeetings()
    {
        await using var db = CreateContext();
        var student = await AddPersonAsync(db, "Podopieczny");
        var catechist = await AddPersonAsync(db, "Katechista");
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = student.Id, CatechistPersonId = catechist.Id, Path = DokPath.Confirmation,
            Stage = DokStage.Evangelization, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.DokCases.Add(dokCase);
        db.Meetings.Add(new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = new DateOnly(2026, 9, 1), CreatedAtUtc = DateTime.UtcNow });
        db.Meetings.Add(new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = new DateOnly(2026, 12, 1), CreatedAtUtc = DateTime.UtcNow });
        var group = new Meeting { Id = Guid.NewGuid(), GroupLabel = "Grupa", MeetingDate = new DateOnly(2026, 10, 5), CreatedAtUtc = DateTime.UtcNow };
        db.Meetings.Add(group);
        db.MeetingAttendees.Add(new MeetingAttendee { Id = Guid.NewGuid(), MeetingId = group.Id, DokCaseId = dokCase.Id });
        await db.SaveChangesAsync();

        var dto = await new DokCaseService(db, time: Time).GetByIdAsync(dokCase.Id, default);

        Assert.Equal(new DateOnly(2026, 10, 5), dto!.LastMeetingDate);
    }
}
