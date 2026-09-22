using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class NameDayServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task GetUpcomingAsync_ExcludesPeopleWithoutNameDayFields()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var withNameDay = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski",
            NameDayMonth = today.Month, NameDayDay = today.Day,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        var withoutNameDay = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.AddRange(withNameDay, withoutNameDay);
        await db.SaveChangesAsync();

        var service = new NameDayService(db);
        var result = await service.GetUpcomingAsync(30, default);

        Assert.Single(result);
        Assert.Equal("Jan Kowalski", result[0].FullName);
        Assert.Equal(0, result[0].DaysUntil);
    }

    [Fact]
    public async Task GetUpcomingAsync_WrapsAroundYearEnd_AndSortsByDaysUntil()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var inTwoDays = today.AddDays(2);
        var farAway = today.AddDays(200);

        db.People.AddRange(
            new Person
            {
                Id = Guid.NewGuid(), FirstName = "Piotr", LastName = "Zima",
                NameDayMonth = inTwoDays.Month, NameDayDay = inTwoDays.Day,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            },
            new Person
            {
                Id = Guid.NewGuid(), FirstName = "Karol", LastName = "Daleki",
                NameDayMonth = farAway.Month, NameDayDay = farAway.Day,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var service = new NameDayService(db);
        var result = await service.GetUpcomingAsync(30, default);

        Assert.Single(result);
        Assert.Equal("Piotr Zima", result[0].FullName);
        Assert.Equal(2, result[0].DaysUntil);
    }
}
