using DokPortal.Application.DokCases;
using DokPortal.Application.Meetings;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Zajęcia cykliczne: jedno zgłoszenie zakłada serię spotkań co tydzień do wskazanej daty.</summary>
public class MeetingSeriesTests
{
    private static readonly DateOnly Start = new(2026, 10, 15);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CreateMeetingRequest Weekly(DateOnly? until) => new()
    {
        GroupLabel = "Grupa czwartkowa", MeetingDate = Start,
        StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(20, 0), RepeatWeeklyUntil = until
    };

    [Fact]
    public async Task CreateAsync_RepeatWeekly_CreatesOneMeetingPerWeekUpToTheEndDate()
    {
        await using var db = CreateContext();
        var service = new MeetingService(db);

        var first = await service.CreateAsync(Weekly(Start.AddDays(30)), default);

        var all = await service.GetAllAsync(default);
        Assert.Equal(new[] { Start, Start.AddDays(7), Start.AddDays(14), Start.AddDays(21), Start.AddDays(28) },
            all.Select(m => m.MeetingDate).OrderBy(d => d).ToArray());
        Assert.All(all, m => Assert.Equal(first.SeriesId, m.SeriesId));
        Assert.All(all, m => Assert.Equal(new TimeOnly(18, 0), m.StartTime));
        Assert.NotNull(first.SeriesId);
        Assert.Equal(Start, first.MeetingDate);
    }

    [Fact]
    public async Task CreateAsync_WithoutRepeat_HasNoSeries()
    {
        await using var db = CreateContext();
        var created = await new MeetingService(db).CreateAsync(Weekly(null), default);
        Assert.Null(created.SeriesId);
    }

    [Fact]
    public async Task CreateAsync_RepeatEndBeforeStart_IsRejected()
    {
        await using var db = CreateContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new MeetingService(db).CreateAsync(Weekly(Start), default));
    }

    [Fact]
    public async Task CreateAsync_RepeatLongerThanAYear_IsRejected()
    {
        await using var db = CreateContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new MeetingService(db).CreateAsync(Weekly(Start.AddDays(400)), default));
    }

    [Fact]
    public async Task DeleteAsync_WithFollowing_RemovesThisAndLaterMeetingsOfTheSeriesOnly()
    {
        await using var db = CreateContext();
        var service = new MeetingService(db);
        await service.CreateAsync(Weekly(Start.AddDays(28)), default);
        var other = await service.CreateAsync(new CreateMeetingRequest { GroupLabel = "Inna", MeetingDate = Start.AddDays(20) }, default);
        var third = (await service.GetAllAsync(default)).Single(m => m.MeetingDate == Start.AddDays(14));

        var deleted = await service.DeleteAsync(third.Id, "admin", default, withFollowing: true);

        Assert.True(deleted);
        var left = (await service.GetAllAsync(default)).Select(m => m.MeetingDate).OrderBy(d => d).ToArray();
        Assert.Equal(new[] { Start, Start.AddDays(7), Start.AddDays(20) }, left);
        Assert.Contains(await service.GetAllAsync(default), m => m.Id == other.Id);
    }

    [Fact]
    public async Task DeleteAsync_WithoutFollowing_RemovesOnlyThatMeeting()
    {
        await using var db = CreateContext();
        var service = new MeetingService(db);
        var first = await service.CreateAsync(Weekly(Start.AddDays(14)), default);

        await service.DeleteAsync(first.Id, "admin", default);

        Assert.Equal(2, (await service.GetAllAsync(default)).Count);
    }
}
