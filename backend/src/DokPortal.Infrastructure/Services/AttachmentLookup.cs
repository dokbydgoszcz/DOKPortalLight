using DokPortal.Application.Attachments;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

/// <summary>Pomocnik dla serwisów, które dołączają listę plików do własnych DTO (notatki, superwizje).</summary>
public static class AttachmentLookup
{
    public static async Task<Dictionary<Guid, List<AttachmentDto>>> ForOwnersAsync(
        AppDbContext db, AttachmentOwnerType ownerType, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct)
    {
        if (ownerIds.Count == 0) return new Dictionary<Guid, List<AttachmentDto>>();

        var rows = await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerType == ownerType && ownerIds.Contains(a.OwnerId))
            .OrderBy(a => a.UploadedAtUtc).ThenBy(a => a.FileName)
            .ToListAsync(ct);
        return rows.GroupBy(a => a.OwnerId).ToDictionary(g => g.Key, g => g.Select(ToDto).ToList());
    }

    public static AttachmentDto ToDto(Attachment a) => new()
    {
        Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, SizeBytes = a.SizeBytes, UploadedAtUtc = a.UploadedAtUtc
    };
}
