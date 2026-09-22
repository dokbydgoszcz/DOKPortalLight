using DokPortal.Application.PastoralNotes;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class PastoralNoteServiceTests
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
    public async Task GetVisibleForCaseAsync_NonPrivilegedUser_SeesOnlyOwnNote()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var caseId = await SeedCaseAsync(db);
        var service = new PastoralNoteService(db);
        await service.CreateAsync(caseId, "author-a", new CreatePastoralNoteRequest { Content = "Notatka A" }, default);
        await service.CreateAsync(caseId, "author-b", new CreatePastoralNoteRequest { Content = "Notatka B" }, default);

        var visibleToA = await service.GetVisibleForCaseAsync(caseId, "author-a", isPrivileged: false, default);

        Assert.Single(visibleToA);
        Assert.Equal("Notatka A", visibleToA[0].Content);
    }

    [Fact]
    public async Task GetVisibleForCaseAsync_PrivilegedUser_SeesAllNotes()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var caseId = await SeedCaseAsync(db);
        var service = new PastoralNoteService(db);
        await service.CreateAsync(caseId, "author-a", new CreatePastoralNoteRequest { Content = "Notatka A" }, default);
        await service.CreateAsync(caseId, "author-b", new CreatePastoralNoteRequest { Content = "Notatka B" }, default);

        var visibleToDirector = await service.GetVisibleForCaseAsync(caseId, "director-user", isPrivileged: true, default);

        Assert.Equal(2, visibleToDirector.Count);
    }
}
