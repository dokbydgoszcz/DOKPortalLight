using DokPortal.Domain.Enums;

namespace DokPortal.Application.Documents;

public class GeneratedDocumentDto
{
    public required Guid Id { get; init; }
    public DocumentTemplate Template { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public required string GeneratedByUserId { get; init; }
    public string? AdditionalNotes { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
