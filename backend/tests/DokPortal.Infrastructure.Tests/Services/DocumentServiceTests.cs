using DokPortal.Application.Documents;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class DocumentServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task GenerateAsync_WhenPersonExists_ReturnsNonEmptyPdfAndSavesHistory()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var service = new DocumentService(db);
        var result = await service.GenerateAsync(
            new GenerateDocumentRequest { Template = DocumentTemplate.LetterToBishop, PersonId = person.Id, AdditionalNotes = "Test" },
            "user-1", default);

        Assert.NotNull(result);
        Assert.NotEmpty(result!.PdfBytes);
        Assert.Equal(1, await db.GeneratedDocuments.CountAsync());
        Assert.Equal("Jan Kowalski", result.History.PersonFullName);
    }

    [Fact]
    public async Task GenerateAsync_WhenPersonMissing_ReturnsNull()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new DocumentService(db);

        var result = await service.GenerateAsync(
            new GenerateDocumentRequest { Template = DocumentTemplate.LetterToBishop, PersonId = Guid.NewGuid() },
            "user-1", default);

        Assert.Null(result);
    }
}
