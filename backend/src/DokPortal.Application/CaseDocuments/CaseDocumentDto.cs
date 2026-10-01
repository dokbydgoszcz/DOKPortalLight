namespace DokPortal.Application.CaseDocuments;

public class CaseDocumentDto
{
    public required Guid Id { get; init; }
    public required Guid DokCaseId { get; init; }
    public required string Name { get; init; }
    public required bool IsProvided { get; init; }
    public string? OriginalFileName { get; init; }
    public long? FileSizeBytes { get; init; }
    public DateTime? UploadedAtUtc { get; init; }
    public bool HasFile => OriginalFileName is not null;
}
