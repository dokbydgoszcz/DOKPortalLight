using DokPortal.Application.Supervisions;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class SupervisionServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenGetAllAsync_FiltersByInstitution()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new SupervisionService(db);

        await service.CreateAsync(new CreateSupervisionRequest
        {
            Institution = Institution.DOK, GroupLabel = "Grupa A", SupervisionDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);
        await service.CreateAsync(new CreateSupervisionRequest
        {
            Institution = Institution.SKSP, GroupLabel = "Grupa B", SupervisionDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);

        var dokOnly = await service.GetAllAsync(Institution.DOK, default);

        Assert.Single(dokOnly);
        Assert.Equal("Grupa A", dokOnly[0].GroupLabel);
    }
}
