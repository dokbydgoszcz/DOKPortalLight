using DokPortal.Application.DokCases;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class DokCaseServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<(Guid personId, Guid catechistId)> SeedPeopleAsync(AppDbContext db)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(person, catechist);
        await db.SaveChangesAsync();
        return (person.Id, catechist.Id);
    }

    [Fact]
    public async Task CreateAsync_ThenSearchAsync_FiltersByPath()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var (personId, catechistId) = await SeedPeopleAsync(db);
        var service = new DokCaseService(db);

        await service.CreateAsync(new CreateDokCaseRequest
        {
            PersonId = personId, Path = DokPath.Confirmation, Stage = DokStage.Formation, CatechistPersonId = catechistId
        }, default);

        var confirmation = await service.SearchAsync(DokPath.Confirmation, 1, 20, default);
        var conversion = await service.SearchAsync(DokPath.Conversion, 1, 20, default);

        Assert.Single(confirmation.Items);
        Assert.Equal("Jan Kowalski", confirmation.Items[0].PersonFullName);
        Assert.Empty(conversion.Items);
    }

    [Fact]
    public async Task UpdateAsync_TransitioningToGraduate_StampsCompletedAtUtc()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var (personId, catechistId) = await SeedPeopleAsync(db);
        var service = new DokCaseService(db);
        var created = await service.CreateAsync(new CreateDokCaseRequest
        {
            PersonId = personId, Path = DokPath.Confirmation, Stage = DokStage.Sacrament, CatechistPersonId = catechistId
        }, default);

        var updated = await service.UpdateAsync(created.Id, new UpdateDokCaseRequest
        {
            PersonId = personId, Path = DokPath.Confirmation, Stage = DokStage.Graduate, CatechistPersonId = catechistId
        }, default);

        Assert.NotNull(updated!.CompletedAtUtc);
    }
}
