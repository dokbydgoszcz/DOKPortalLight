using DokPortal.Application.Parishes;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class ParishServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenGetAllAsync_ReturnsCreatedParish()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new ParishService(db);

        await service.CreateAsync(new CreateParishRequest { Name = "św. Pawła", City = "Bydgoszcz" }, default);
        var all = await service.GetAllAsync(default);

        Assert.Contains(all, p => p.Name == "św. Pawła" && p.City == "Bydgoszcz");
    }
}
