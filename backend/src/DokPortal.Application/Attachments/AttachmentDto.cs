namespace DokPortal.Application.Attachments;

public class AttachmentDto
{
    public required Guid Id { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime UploadedAtUtc { get; init; }
}
