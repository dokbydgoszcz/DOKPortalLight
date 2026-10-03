using DokPortal.Application.Attachments;
using System.Text;
using DokPortal.Application.CaseDocuments;
using DokPortal.Application.Common;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class CaseDocumentServiceFileTests
{
    private class InMemoryFileStorage : IFileStorageService
    {
        public Dictionary<string, (byte[] Content, string ContentType)> Files { get; } = new();

        public async Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            Files[blobPath] = (buffer.ToArray(), contentType);
        }

        public Task<StoredFile?> DownloadAsync(string blobPath, CancellationToken ct) =>
            Task.FromResult<StoredFile?>(Files.TryGetValue(blobPath, out var file)
                ? new StoredFile(new MemoryStream(file.Content), file.ContentType)
                : null);

        public Task DeleteAsync(string blobPath, CancellationToken ct)
        {
            Files.Remove(blobPath);
            return Task.CompletedTask;
        }
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(CaseDocumentService Service, InMemoryFileStorage Storage, Guid CaseId, Guid DocumentId, AppDbContext Db)> SeedAsync()
    {
        var db = CreateContext();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(person, catechist);
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id,
            Path = DokPath.Confirmation, Stage = DokStage.Formation, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.DokCases.Add(dokCase);
        await db.SaveChangesAsync();
        var storage = new InMemoryFileStorage();
        var service = new CaseDocumentService(db, storage);
        var document = await service.CreateAsync(dokCase.Id, new CreateCaseDocumentRequest { Name = "Metryka chrztu" }, default);
        return (service, storage, dokCase.Id, document.Id, db);
    }

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task UploadFileAsync_StoresTheFile_MarksTheDocumentAsProvidedAndRecordsFileInfo()
    {
        var (service, storage, caseId, documentId, db) = await SeedAsync();
        using var content = Bytes("PDF-DATA");

        var result = await service.UploadFileAsync(caseId, documentId, content, "metryka.pdf", "application/pdf", 8, default);

        Assert.NotNull(result);
        Assert.True(result!.IsProvided);
        Assert.Equal("metryka.pdf", result.OriginalFileName);
        Assert.Equal(8, result.FileSizeBytes);
        Assert.NotNull(result.UploadedAtUtc);
        var blobPath = $"case-documents/{caseId}/{documentId}/metryka.pdf";
        Assert.Equal("PDF-DATA", Encoding.UTF8.GetString(storage.Files[blobPath].Content));
        Assert.Equal("application/pdf", storage.Files[blobPath].ContentType);
        Assert.Equal(blobPath, (await db.CaseDocuments.SingleAsync()).BlobPath);
    }

    [Fact]
    public async Task UploadFileAsync_ReturnsNull_ForUnknownDocumentOrDocumentOfAnotherCase()
    {
        var (service, storage, caseId, documentId, _) = await SeedAsync();
        using var content = Bytes("x");

        var unknown = await service.UploadFileAsync(caseId, Guid.NewGuid(), content, "a.pdf", "application/pdf", 1, default);
        var otherCase = await service.UploadFileAsync(Guid.NewGuid(), documentId, content, "a.pdf", "application/pdf", 1, default);

        Assert.Null(unknown);
        Assert.Null(otherCase);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task DownloadFileAsync_ReturnsTheStoredFileWithItsOriginalName()
    {
        var (service, _, caseId, documentId, _) = await SeedAsync();
        using var content = Bytes("PDF-DATA");
        await service.UploadFileAsync(caseId, documentId, content, "metryka.pdf", "application/pdf", 8, default);

        var downloaded = await service.DownloadFileAsync(caseId, documentId, default);

        Assert.NotNull(downloaded);
        Assert.Equal("metryka.pdf", downloaded!.Value.FileName);
        Assert.Equal("application/pdf", downloaded.Value.File.ContentType);
        using var reader = new StreamReader(downloaded.Value.File.Content);
        Assert.Equal("PDF-DATA", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task DownloadFileAsync_ReturnsNull_WhenNothingWasUploadedOrTheBlobIsGone()
    {
        var (service, storage, caseId, documentId, _) = await SeedAsync();

        Assert.Null(await service.DownloadFileAsync(caseId, documentId, default));
        Assert.Null(await service.DownloadFileAsync(caseId, Guid.NewGuid(), default));

        using var content = Bytes("x");
        await service.UploadFileAsync(caseId, documentId, content, "a.pdf", "application/pdf", 1, default);
        storage.Files.Clear();
        Assert.Null(await service.DownloadFileAsync(caseId, documentId, default));
    }

    [Fact]
    public async Task SetProvidedAsync_ReturnsNull_ForUnknownDocumentOrDocumentOfAnotherCase()
    {
        var (service, _, caseId, documentId, _) = await SeedAsync();

        Assert.Null(await service.SetProvidedAsync(caseId, Guid.NewGuid(), true, default));
        Assert.Null(await service.SetProvidedAsync(Guid.NewGuid(), documentId, true, default));
        var cleared = await service.SetProvidedAsync(caseId, documentId, false, default);
        Assert.False(cleared!.IsProvided);
    }

    [Fact]
    public async Task UploadFileAsync_RejectsATypeTheRulesDoNotAllow_AndStoresNothing()
    {
        var (service, storage, caseId, documentId, db) = await SeedAsync();
        using var content = Bytes("MZ");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadFileAsync(caseId, documentId, content, "arkusz.xlsx", "application/vnd.ms-excel", 2, default));

        Assert.Contains("PDF", ex.Message);
        Assert.Empty(storage.Files);
        Assert.Null((await db.CaseDocuments.SingleAsync()).BlobPath);
    }

    [Fact]
    public async Task UploadFileAsync_RejectsTooBigFiles()
    {
        var (service, storage, caseId, documentId, _) = await SeedAsync();
        using var content = Bytes("x");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadFileAsync(caseId, documentId, content, "a.pdf", "application/pdf", AttachmentRules.MaxFileBytes + 1, default));

        Assert.Contains("20 MB", ex.Message);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task UploadFileAsync_StoresAContentTypeFromTheExtension_NotFromTheClient()
    {
        var (service, storage, caseId, documentId, db) = await SeedAsync();
        using var content = Bytes("x");

        await service.UploadFileAsync(caseId, documentId, content, "zdjecie.JPG", "application/octet-stream", 1, default);

        Assert.Equal("image/jpeg", (await db.CaseDocuments.SingleAsync()).ContentType);
        Assert.Equal("image/jpeg", storage.Files.Single().Value.ContentType);
    }
}
