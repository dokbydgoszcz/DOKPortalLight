namespace DokPortal.Domain.Entities;

public class CaseDocument
{
    public Guid Id { get; set; }
    public Guid DokCaseId { get; set; }
    public DokCase? DokCase { get; set; }
    public required string Name { get; set; }
    public bool IsProvided { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public string? BlobPath { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime? UploadedAtUtc { get; set; }
}
