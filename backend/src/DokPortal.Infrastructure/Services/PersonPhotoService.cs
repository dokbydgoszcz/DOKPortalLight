using DokPortal.Application.Common;
using DokPortal.Application.People;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class PersonPhotoService : IPersonPhotoService
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _storage;

    public PersonPhotoService(AppDbContext db, IFileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    /// <summary>Typ obrazu rozpoznany po pierwszych bajtach pliku (nie po nazwie ani nagłówku przeglądarki).</summary>
    public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return ("image/jpeg", ".jpg");
        if (head.Length >= 8 && head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ("image/png", ".png");
        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8)) return ("image/webp", ".webp");
        return null;
    }

    public async Task<bool> SaveAsync(Guid personId, Stream content, long sizeBytes, CancellationToken ct)
    {
        if (sizeBytes <= 0) throw new InvalidOperationException("Plik jest pusty.");
        if (sizeBytes > IPersonPhotoService.MaxBytes) throw new InvalidOperationException("Zdjęcie jest za duże – maksymalnie 5 MB.");
        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == personId, ct);
        if (person is null) return false;

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var kind = Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)))
            ?? throw new InvalidOperationException("Zdjęcie musi być w formacie JPG, PNG lub WEBP.");

        var oldPath = person.PhotoBlobPath;
        var path = $"people/{personId}/photo-{Guid.NewGuid():N}{kind.Extension}";
        buffer.Position = 0;
        await _storage.UploadAsync(path, buffer, kind.ContentType, ct);
        person.PhotoBlobPath = path;
        person.PhotoContentType = kind.ContentType;
        person.PhotoUpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (oldPath is not null) await _storage.DeleteAsync(oldPath, ct);
        return true;
    }

    public async Task<StoredFile?> GetAsync(Guid personId, CancellationToken ct)
    {
        var path = await _db.People.Where(p => p.Id == personId).Select(p => p.PhotoBlobPath).FirstOrDefaultAsync(ct);
        return path is null ? null : await _storage.DownloadAsync(path, ct);
    }

    public async Task<bool> DeleteAsync(Guid personId, CancellationToken ct)
    {
        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == personId, ct);
        if (person?.PhotoBlobPath is null) return false;
        var path = person.PhotoBlobPath;
        person.PhotoBlobPath = null;
        person.PhotoContentType = null;
        person.PhotoUpdatedAtUtc = null;
        await _db.SaveChangesAsync(ct);
        await _storage.DeleteAsync(path, ct);
        return true;
    }
}
