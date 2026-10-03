using DokPortal.Application.Attachments;
using DokPortal.Application.Common;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class AttachmentService : IAttachmentService
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _storage;

    public AttachmentService(AppDbContext db, IFileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(AttachmentOwnerType ownerType, Guid ownerId, CancellationToken ct)
    {
        var grouped = await AttachmentLookup.ForOwnersAsync(_db, ownerType, new[] { ownerId }, ct);
        return grouped.GetValueOrDefault(ownerId) ?? new List<AttachmentDto>();
    }

    public async Task<AttachmentDto> AddAsync(
        AttachmentOwnerType ownerType, Guid ownerId, Stream content, string fileName, long sizeBytes, string uploadedByUserId, CancellationToken ct)
    {
        var cleanName = AttachmentRules.CleanFileName(fileName);
        var error = AttachmentRules.Validate(cleanName, sizeBytes);
        if (error is not null) throw new InvalidOperationException(error);

        var id = Guid.NewGuid();
        var contentType = AttachmentRules.ContentTypeFor(cleanName);
        var extension = Path.GetExtension(cleanName).ToLowerInvariant();
        var blobPath = $"attachments/{ownerType}/{ownerId}/{id}{extension}";

        await _storage.UploadAsync(blobPath, content, contentType, ct);

        var attachment = new Attachment
        {
            Id = id, OwnerType = ownerType, OwnerId = ownerId, FileName = cleanName, ContentType = contentType,
            SizeBytes = sizeBytes, BlobPath = blobPath, UploadedByUserId = uploadedByUserId, UploadedAtUtc = DateTime.UtcNow
        };
        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync(ct);
        return AttachmentLookup.ToDto(attachment);
    }

    public async Task<(StoredFile File, string FileName)?> DownloadAsync(
        AttachmentOwnerType ownerType, Guid ownerId, Guid attachmentId, CancellationToken ct)
    {
        var attachment = await _db.Attachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.OwnerType == ownerType && a.OwnerId == ownerId, ct);
        if (attachment is null) return null;

        var file = await _storage.DownloadAsync(attachment.BlobPath, ct);
        return file is null ? null : (file, attachment.FileName);
    }

    public async Task<bool> DeleteAsync(AttachmentOwnerType ownerType, Guid ownerId, Guid attachmentId, CancellationToken ct)
    {
        var attachment = await _db.Attachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.OwnerType == ownerType && a.OwnerId == ownerId, ct);
        if (attachment is null) return false;

        _db.Attachments.Remove(attachment);
        await _db.SaveChangesAsync(ct);

        // Rekord jest już usunięty; awaria magazynu zostawia co najwyżej osierocony plik, ale nie blokuje użytkownika.
        try
        {
            await _storage.DeleteAsync(attachment.BlobPath, ct);
        }
        catch (Exception)
        {
        }
        return true;
    }
}
