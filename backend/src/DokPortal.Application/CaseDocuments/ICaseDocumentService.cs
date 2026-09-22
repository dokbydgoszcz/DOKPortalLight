namespace DokPortal.Application.CaseDocuments;

public interface ICaseDocumentService
{
    Task<IReadOnlyList<CaseDocumentDto>> GetForCaseAsync(Guid caseId, CancellationToken ct);
    Task<CaseDocumentDto> CreateAsync(Guid caseId, CreateCaseDocumentRequest request, CancellationToken ct);
    Task<CaseDocumentDto?> SetProvidedAsync(Guid caseId, Guid documentId, bool isProvided, CancellationToken ct);
}
