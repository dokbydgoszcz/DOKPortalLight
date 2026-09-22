using DokPortal.Application.Meetings;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class MeetingServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_GroupSession_ThenGetAllAsync_ReturnsGroupLabelWithoutCaseLabel()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new MeetingService(db);

        await service.CreateAsync(new CreateMeetingRequest
        {
            GroupLabel = "DOK grupa", MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);

        var meetings = await service.GetAllAsync(default);

        Assert.Single(meetings);
        Assert.Equal("DOK grupa", meetings[0].GroupLabel);
        Assert.Null(meetings[0].CaseLabel);
    }

    [Fact]
    public async Task CreateAsync_IndividualMeeting_ReturnsCaseLabel()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(person, catechist);
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = person.Id, Path = DokPath.Confirmation, Stage = DokStage.Formation,
            CatechistPersonId = catechist.Id, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.DokCases.Add(dokCase);
        await db.SaveChangesAsync();
        var service = new MeetingService(db);

        await service.CreateAsync(new CreateMeetingRequest
        {
            DokCaseId = dokCase.Id, MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow), IsAttended = true
        }, default);

        var meetings = await service.GetAllAsync(default);

        Assert.Single(meetings);
        Assert.Equal("Jan Kowalski", meetings[0].CaseLabel);
    }
}
