using DokPortal.Application.CaseDocuments;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class CaseDocumentServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<Guid> SeedCaseAsync(AppDbContext db)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(person, catechist);
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = person.Id, Path = DokPath.Confirmation, Stage = DokStage.Formation,
            CatechistPersonId = catechist.Id, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.DokCases.Add(dokCase);
        await db.SaveChangesAsync();
        return dokCase.Id;
    }

    [Fact]
    public async Task CreateAsync_ThenSetProvidedAsync_TogglesStatus()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var caseId = await SeedCaseAsync(db);
        var service = new CaseDocumentService(db);

        var created = await service.CreateAsync(caseId, new CreateCaseDocumentRequest { Name = "Metryka chrztu" }, default);
        var updated = await service.SetProvidedAsync(caseId, created.Id, true, default);

        Assert.NotNull(updated);
        Assert.True(updated!.IsProvided);
    }

    [Fact]
    public async Task GetForCaseAsync_ReturnsOnlyDocumentsForThatCase()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var caseId = await SeedCaseAsync(db);
        var otherCaseId = await SeedCaseAsync(db);
        var service = new CaseDocumentService(db);
        await service.CreateAsync(caseId, new CreateCaseDocumentRequest { Name = "Metryka chrztu" }, default);
        await service.CreateAsync(otherCaseId, new CreateCaseDocumentRequest { Name = "Inny dokument" }, default);

        var documents = await service.GetForCaseAsync(caseId, default);

        Assert.Single(documents);
        Assert.Equal("Metryka chrztu", documents[0].Name);
    }
}
