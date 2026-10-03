using System.Text;
using DokPortal.Application.Attachments;
using DokPortal.Application.Common;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class AttachmentServiceTests
{
    private class InMemoryFileStorage : IFileStorageService
    {
        public Dictionary<string, (byte[] Content, string ContentType)> Files { get; } = new();
        public bool FailOnUpload { get; set; }
        public bool FailOnDelete { get; set; }

        public async Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct)
        {
            if (FailOnUpload) throw new InvalidOperationException("storage down");
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
            if (FailOnDelete) throw new InvalidOperationException("storage down");
            Files.Remove(blobPath);
            return Task.CompletedTask;
        }
    }

    private static (AttachmentService Service, InMemoryFileStorage Storage, AppDbContext Db) Create()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var storage = new InMemoryFileStorage();
        return (new AttachmentService(db, storage), storage, db);
    }

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task AddAsync_StoresTheFile_AndRecordsWhoAddedItAndWhen()
    {
        var (service, storage, db) = Create();
        var ownerId = Guid.NewGuid();
        using var content = Bytes("PDF-DATA");

        var dto = await service.AddAsync(AttachmentOwnerType.PastoralNote, ownerId, content, @"C:\skany\notatka.PDF", 8, "user-1", default);

        Assert.Equal("notatka.PDF", dto.FileName);
        Assert.Equal(8, dto.SizeBytes);
        Assert.Equal("application/pdf", dto.ContentType);
        var row = await db.Attachments.SingleAsync();
        Assert.Equal(AttachmentOwnerType.PastoralNote, row.OwnerType);
        Assert.Equal(ownerId, row.OwnerId);
        Assert.Equal("user-1", row.UploadedByUserId);
        Assert.StartsWith($"attachments/PastoralNote/{ownerId}/{row.Id}", row.BlobPath);
        Assert.EndsWith(".pdf", row.BlobPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("PDF-DATA", Encoding.UTF8.GetString(storage.Files[row.BlobPath].Content));
    }

    [Fact]
    public async Task AddAsync_UsesAContentTypeFromTheExtension_NotFromTheClient()
    {
        var (service, storage, db) = Create();
        using var content = Bytes("x");

        await service.AddAsync(AttachmentOwnerType.Supervision, Guid.NewGuid(), content, "notatka.txt", 1, "u", default);

        var row = await db.Attachments.SingleAsync();
        Assert.Equal("text/plain; charset=utf-8", row.ContentType);
        Assert.Equal("text/plain; charset=utf-8", storage.Files[row.BlobPath].ContentType);
    }

    [Fact]
    public async Task AddAsync_RejectsAFileTheRulesDoNotAllow_AndStoresNothing()
    {
        var (service, storage, db) = Create();
        using var content = Bytes("MZ");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddAsync(AttachmentOwnerType.PastoralNote, Guid.NewGuid(), content, "virus.exe", 2, "u", default));

        Assert.Contains("PDF", ex.Message);
        Assert.Empty(storage.Files);
        Assert.Empty(db.Attachments);
    }

    [Fact]
    public async Task AddAsync_RejectsTooBigFiles()
    {
        var (service, storage, _) = Create();
        using var content = Bytes("x");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddAsync(AttachmentOwnerType.PastoralNote, Guid.NewGuid(), content, "a.pdf", AttachmentRules.MaxFileBytes + 1, "u", default));

        Assert.Contains("20 MB", ex.Message);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task AddAsync_WhenTheStorageFails_NothingIsRecorded()
    {
        var (service, storage, db) = Create();
        storage.FailOnUpload = true;
        using var content = Bytes("x");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddAsync(AttachmentOwnerType.PastoralNote, Guid.NewGuid(), content, "a.pdf", 1, "u", default));

        Assert.Empty(db.Attachments);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyTheOwnersFiles_OldestFirst()
    {
        var (service, _, _) = Create();
        var owner = Guid.NewGuid();
        using (var a = Bytes("1")) await service.AddAsync(AttachmentOwnerType.PastoralNote, owner, a, "a.pdf", 1, "u", default);
        using (var b = Bytes("2")) await service.AddAsync(AttachmentOwnerType.PastoralNote, owner, b, "b.pdf", 1, "u", default);
        using (var c = Bytes("3")) await service.AddAsync(AttachmentOwnerType.PastoralNote, Guid.NewGuid(), c, "other.pdf", 1, "u", default);
        using (var d = Bytes("4")) await service.AddAsync(AttachmentOwnerType.Supervision, owner, d, "sup.pdf", 1, "u", default);

        var list = await service.ListAsync(AttachmentOwnerType.PastoralNote, owner, default);

        Assert.Equal(new[] { "a.pdf", "b.pdf" }, list.Select(x => x.FileName));
    }

    [Fact]
    public async Task DownloadAsync_ReturnsTheStoredFileWithItsName()
    {
        var (service, _, _) = Create();
        var owner = Guid.NewGuid();
        using var content = Bytes("TRESC");
        var dto = await service.AddAsync(AttachmentOwnerType.Supervision, owner, content, "protokol.txt", 5, "u", default);

        var downloaded = await service.DownloadAsync(AttachmentOwnerType.Supervision, owner, dto.Id, default);

        Assert.NotNull(downloaded);
        Assert.Equal("protokol.txt", downloaded!.Value.FileName);
        using var reader = new StreamReader(downloaded.Value.File.Content);
        Assert.Equal("TRESC", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task DownloadAsync_ReturnsNull_ForAnotherOwnerTypeOrOwnerOrWhenTheBlobIsGone()
    {
        var (service, storage, _) = Create();
        var owner = Guid.NewGuid();
        using var content = Bytes("x");
        var dto = await service.AddAsync(AttachmentOwnerType.PastoralNote, owner, content, "a.pdf", 1, "u", default);

        Assert.Null(await service.DownloadAsync(AttachmentOwnerType.Supervision, owner, dto.Id, default));
        Assert.Null(await service.DownloadAsync(AttachmentOwnerType.PastoralNote, Guid.NewGuid(), dto.Id, default));
        Assert.Null(await service.DownloadAsync(AttachmentOwnerType.PastoralNote, owner, Guid.NewGuid(), default));
        storage.Files.Clear();
        Assert.Null(await service.DownloadAsync(AttachmentOwnerType.PastoralNote, owner, dto.Id, default));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheRecordAndTheFile()
    {
        var (service, storage, db) = Create();
        var owner = Guid.NewGuid();
        using var content = Bytes("x");
        var dto = await service.AddAsync(AttachmentOwnerType.PastoralNote, owner, content, "a.pdf", 1, "u", default);

        var deleted = await service.DeleteAsync(AttachmentOwnerType.PastoralNote, owner, dto.Id, default);

        Assert.True(deleted);
        Assert.Empty(db.Attachments);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_ForAnotherOwner_AndKeepsTheFile()
    {
        var (service, storage, db) = Create();
        var owner = Guid.NewGuid();
        using var content = Bytes("x");
        var dto = await service.AddAsync(AttachmentOwnerType.PastoralNote, owner, content, "a.pdf", 1, "u", default);

        Assert.False(await service.DeleteAsync(AttachmentOwnerType.PastoralNote, Guid.NewGuid(), dto.Id, default));
        Assert.False(await service.DeleteAsync(AttachmentOwnerType.Supervision, owner, dto.Id, default));

        Assert.Single(db.Attachments);
        Assert.Single(storage.Files);
    }

    [Fact]
    public async Task DeleteAsync_StillRemovesTheRecord_WhenTheStorageFails()
    {
        var (service, storage, db) = Create();
        var owner = Guid.NewGuid();
        using var content = Bytes("x");
        var dto = await service.AddAsync(AttachmentOwnerType.PastoralNote, owner, content, "a.pdf", 1, "u", default);
        storage.FailOnDelete = true;

        Assert.True(await service.DeleteAsync(AttachmentOwnerType.PastoralNote, owner, dto.Id, default));

        Assert.Empty(db.Attachments);
    }

    [Fact]
    public async Task ListForOwnersAsync_GroupsFilesPerOwner_ForTheGivenType()
    {
        var (service, _, db) = Create();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        using (var a = Bytes("1")) await service.AddAsync(AttachmentOwnerType.PastoralNote, first, a, "a.pdf", 1, "u", default);
        using (var b = Bytes("2")) await service.AddAsync(AttachmentOwnerType.PastoralNote, first, b, "b.pdf", 1, "u", default);
        using (var c = Bytes("3")) await service.AddAsync(AttachmentOwnerType.PastoralNote, second, c, "c.pdf", 1, "u", default);
        using (var d = Bytes("4")) await service.AddAsync(AttachmentOwnerType.Supervision, first, d, "sup.pdf", 1, "u", default);

        var grouped = await AttachmentLookup.ForOwnersAsync(db, AttachmentOwnerType.PastoralNote, new[] { first, second, Guid.NewGuid() }, default);

        Assert.Equal(2, grouped[first].Count);
        Assert.Single(grouped[second]);
        Assert.Equal(2, grouped.Count);
    }
}
