using DokPortal.Application.Formators;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class FormatorServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenGetAllAsync_ReturnsCreatedFormator()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Joanna", LastName = "Lis",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        var service = new FormatorService(db);

        await service.CreateAsync(new CreateFormatorRequest { PersonId = person.Id, Function = "Wykładowca" }, default);
        var all = await service.GetAllAsync(default);

        Assert.Contains(all, f => f.PersonFullName == "Joanna Lis" && f.Function == "Wykładowca");
    }
}
