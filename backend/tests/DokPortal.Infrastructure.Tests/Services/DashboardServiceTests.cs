using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_CountsPeopleAndParishes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        db.Parishes.Add(new Parish { Id = Guid.NewGuid(), Name = "św. Pawła" });
        db.People.Add(new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski" });
        await db.SaveChangesAsync();

        var service = new DashboardService(db);
        var summary = await service.GetSummaryAsync(default);

        Assert.Equal(1, summary.PeopleCount);
        Assert.Equal(1, summary.ParishCount);
    }
}
