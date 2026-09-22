using DokPortal.Application.People;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class PersonServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenSearchAsync_FindsPersonByPartialName()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new PersonService(db);

        await service.CreateAsync(new CreatePersonRequest { FirstName = "Maria", LastName = "Kaczmarek" }, default);

        var result = await service.SearchAsync("kaczma", 1, 20, default);

        Assert.Single(result.Items);
        Assert.Equal("Maria Kaczmarek", result.Items[0].FullName);
    }

    [Fact]
    public async Task UpdateAsync_WhenPersonMissing_ReturnsNull()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new PersonService(db);

        var result = await service.UpdateAsync(
            Guid.NewGuid(), new UpdatePersonRequest { FirstName = "X", LastName = "Y" }, default);

        Assert.Null(result);
    }
}
