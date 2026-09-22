using DokPortal.Domain.Enums;

namespace DokPortal.Application.Documents;

public class GenerateDocumentRequest
{
    public DocumentTemplate Template { get; init; }
    public Guid PersonId { get; init; }
    public string? AdditionalNotes { get; init; }
}
