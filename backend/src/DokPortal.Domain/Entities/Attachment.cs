using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

/// <summary>Plik dołączony do notatki duszpasterskiej lub superwizji; treść leży w magazynie plików pod <see cref="BlobPath"/>.</summary>
public class Attachment
{
    public Guid Id { get; set; }
    public AttachmentOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string BlobPath { get; set; }
    public required string UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}
