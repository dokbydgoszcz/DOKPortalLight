using DokPortal.Domain.Enums;

namespace DokPortal.Application.Documents;

public class GeneratedDocumentDto
{
    public required Guid Id { get; init; }
    public DocumentTemplate? Template { get; init; }
    public Guid? LetterTemplateId { get; init; }
    /// <summary>Nazwa pisma (wzoru w chwili generowania).</summary>
    public required string Title { get; init; }
    /// <summary>Czy da się pobrać także wersję Word (pisma z wzorów portalu).</summary>
    public bool CanDownloadWord { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public required string GeneratedByUserId { get; init; }
    public string? AdditionalNotes { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public bool HasStoredFile { get; init; }
    public int DownloadCount { get; init; }
}
