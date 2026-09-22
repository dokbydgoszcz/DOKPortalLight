namespace DokPortal.Application.CaseDocuments;

public class CaseDocumentDto
{
    public required Guid Id { get; init; }
    public required Guid DokCaseId { get; init; }
    public required string Name { get; init; }
    public required bool IsProvided { get; init; }
}
