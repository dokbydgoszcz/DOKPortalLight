using System.Text;
using DokPortal.Application.Common;
using DokPortal.Application.Documents;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class DocumentStorageTests
{
    private class FakeStorage : IFileStorageService
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public bool FailUploads { get; set; }
        public bool FailDeletes { get; set; }

        public async Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct)
        {
            if (FailUploads) throw new InvalidOperationException("storage down");
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            Files[blobPath] = buffer.ToArray();
        }

        public Task<StoredFile?> DownloadAsync(string blobPath, CancellationToken ct) =>
            Task.FromResult<StoredFile?>(Files.TryGetValue(blobPath, out var bytes) ? new StoredFile(new MemoryStream(bytes), "application/pdf") : null);

        public Task DeleteAsync(string blobPath, CancellationToken ct)
        {
            if (FailDeletes) throw new InvalidOperationException("storage down");
            Files.Remove(blobPath);
            return Task.CompletedTask;
        }
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Person> SeedPersonAsync(AppDbContext db)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person;
    }

    private static GenerateDocumentRequest Request(Guid personId) => new()
    {
        Template = DocumentTemplate.LetterToBishop, PersonId = personId, AdditionalNotes = "Uwagi"
    };

    private static bool IsPdf(byte[] bytes) => Encoding.ASCII.GetString(bytes, 0, 4) == "%PDF";

    [Fact]
    public async Task GenerateAsync_StoresTheRenderedPdf_AndStartsTheDownloadCountAtOne()
    {
        await using var db = CreateContext();
        var person = await SeedPersonAsync(db);
        var storage = new FakeStorage();
        var service = new DocumentService(db, storage);

        var result = await service.GenerateAsync(Request(person.Id), "user-1", default);

        var stored = Assert.Single(storage.Files);
        Assert.Equal(result!.PdfBytes, stored.Value);
        Assert.StartsWith($"generated-documents/{result.History.Id}/", stored.Key);
        Assert.True(result.History.HasStoredFile);
        Assert.Equal(1, result.History.DownloadCount);
    }

    [Fact]
    public async Task GenerateAsync_StillReturnsThePdf_WhenTheStorageFails()
    {
        await using var db = CreateContext();
        var person = await SeedPersonAsync(db);
        var service = new DocumentService(db, new FakeStorage { FailUploads = true });

        var result = await service.GenerateAsync(Request(person.Id), "user-1", default);

        Assert.True(IsPdf(result!.PdfBytes));
        Assert.False(result.History.HasStoredFile);
        Assert.Single(await db.GeneratedDocuments.ToListAsync());
    }

    [Fact]
    public async Task GenerateAsync_WithoutAStorage_Works_AndStoresNothing()
    {
        await using var db = CreateContext();
        var person = await SeedPersonAsync(db);

        var result = await new DocumentService(db).GenerateAsync(Request(person.Id), "user-1", default);

        Assert.True(IsPdf(result!.PdfBytes));
        Assert.False(result.History.HasStoredFile);
    }

    [Fact]
    public async Task DownloadAsync_ReturnsTheStoredFile_AndCountsTheDownload()
    {
        await using var db = CreateContext();
        var person = await SeedPersonAsync(db);
        var service = new DocumentService(db, new FakeStorage());
        var generated = await service.GenerateAsync(Request(person.Id), "user-1", default);

        var first = await service.DownloadAsync(generated!.History.Id, default);
        var second = await service.DownloadAsync(generated.History.Id, default);

        Assert.Equal(generated.PdfBytes, first!.PdfBytes);
        Assert.False(first.Restored);
        Assert.Equal("LetterToBishop.pdf", first.FileName);
        Assert.Equal(generated.PdfBytes, second!.PdfBytes);
        Assert.Equal(3, (await service.GetHistoryAsync(default)).Single().DownloadCount);
    }

    [Fact]
    public async Task DownloadAsync_RestoresThePdf_ForEntriesWithoutAStoredFile()
    {
        await using var db = CreateContext();
        var person = await SeedPersonAsync(db);
        var old = new GeneratedDocument
        {
            Id = Guid.NewGuid(), Template = DocumentTemplate.DokReferral, PersonId = person.Id, GeneratedByUserId = "user-1",
            CreatedAtUtc = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), DownloadCount = 1
        };
        db.GeneratedDocuments.Add(old);
        await db.SaveChangesAsync();
        var service = new DocumentService(db, new FakeStorage());

        var download = await service.DownloadAsync(old.Id, default);

        Assert.True(download!.Restored);
        Assert.True(IsPdf(download.PdfBytes));
        Assert.Equal("DokReferral.pdf", download.FileName);
        Assert.Equal(2, (await service.GetHistoryAsync(default)).Single().DownloadCount);
    }

    [Fact]
    public async Task DownloadAsync_RestoresThePdf_WhenTheStoredFileIsGone()
    {
        await using var db = CreateContext();
        var person = await SeedPersonAsync(db);
        var storage = new FakeStorage();
        var service = new DocumentService(db, storage);
        var generated = await service.GenerateAsync(Request(person.Id), "user-1", default);
        storage.Files.Clear();

        var download = await service.DownloadAsync(generated!.History.Id, default);

        Assert.True(download!.Restored);
        Assert.True(IsPdf(download.PdfBytes));
    }

    [Fact]
    public async Task DownloadAsync_ReturnsNull_ForAnUnknownDocument()
    {
        await using var db = CreateContext();

        Assert.Null(await new DocumentService(db, new FakeStorage()).DownloadAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheEntryAndTheStoredFile()
    {
        await using var db = CreateContext();
        var person = await SeedPersonAsync(db);
        var storage = new FakeStorage();
        var service = new DocumentService(db, storage);
        var generated = await service.GenerateAsync(Request(person.Id), "user-1", default);

        var removed = await service.DeleteAsync(generated!.History.Id, default);

        Assert.Equal(generated.History.Id, removed!.Id);
        Assert.Equal("Jan Kowalski", removed.PersonFullName);
        Assert.Empty(await db.GeneratedDocuments.ToListAsync());
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task DeleteAsync_StillRemovesTheEntry_WhenTheStorageCannotDeleteTheFile()
    {
        await using var db = CreateContext();
        var person = await SeedPersonAsync(db);
        var storage = new FakeStorage();
        var service = new DocumentService(db, storage);
        var generated = await service.GenerateAsync(Request(person.Id), "user-1", default);
        storage.FailDeletes = true;

        var removed = await service.DeleteAsync(generated!.History.Id, default);

        Assert.NotNull(removed);
        Assert.Empty(await db.GeneratedDocuments.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNull_ForAnUnknownDocument()
    {
        await using var db = CreateContext();

        Assert.Null(await new DocumentService(db, new FakeStorage()).DeleteAsync(Guid.NewGuid(), default));
    }
}
