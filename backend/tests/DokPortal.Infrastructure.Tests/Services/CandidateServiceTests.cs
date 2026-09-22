using DokPortal.Application.Candidates;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class CandidateServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<Guid> SeedPersonAsync(AppDbContext db, string firstName, string lastName)
    {
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = firstName, LastName = lastName,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person.Id;
    }

    [Fact]
    public async Task CreateAsync_ThenSearchAsync_FiltersByYear()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var personId = await SeedPersonAsync(db, "Agnieszka", "Lewandowska");
        var service = new CandidateService(db);

        await service.CreateAsync(new CreateCandidateRequest
        {
            PersonId = personId, Year = 3, AttendancePercentage = 94, OpinionsCollected = 2, IsRetreatCompleted = true
        }, default);

        var yearThree = await service.SearchAsync(3, 1, 20, default);
        var yearOne = await service.SearchAsync(1, 1, 20, default);

        Assert.Single(yearThree.Items);
        Assert.Equal("Agnieszka Lewandowska", yearThree.Items[0].PersonFullName);
        Assert.Empty(yearOne.Items);
    }

    [Fact]
    public async Task UpdateAsync_WhenCandidateMissing_ReturnsNull()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new CandidateService(db);

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateCandidateRequest
        {
            PersonId = Guid.NewGuid(), Year = 1, OpinionsCollected = 0, IsRetreatCompleted = false
        }, default);

        Assert.Null(result);
    }
}
