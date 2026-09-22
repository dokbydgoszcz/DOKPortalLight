using DokPortal.Application.ParishNeeds;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class ParishNeedServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenAssignAsync_SetsAssignedPersonAndStatus()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var parish = new Parish { Id = Guid.NewGuid(), Name = "św. Mateusza" };
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Marek", LastName = "Zielinski",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.Parishes.Add(parish);
        db.People.Add(person);
        await db.SaveChangesAsync();
        var service = new ParishNeedService(db);

        var created = await service.CreateAsync(new CreateParishNeedRequest
        {
            ParishId = parish.Id, Description = "Katechista do przygotowania dorosłych"
        }, default);

        var assigned = await service.AssignAsync(created.Id, person.Id, default);

        Assert.NotNull(assigned);
        Assert.Equal("Marek Zielinski", assigned!.AssignedPersonName);
        Assert.Equal("Assigned", assigned.Status);
    }

    [Fact]
    public async Task AssignAsync_WhenNeedMissing_ReturnsNull()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new ParishNeedService(db);

        var result = await service.AssignAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.Null(result);
    }
}
