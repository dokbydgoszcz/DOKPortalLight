using DokPortal.Application.Common;

namespace DokPortal.Application.CaseDocuments;

public interface ICaseDocumentService
{
    Task<IReadOnlyList<CaseDocumentDto>> GetForCaseAsync(Guid caseId, CancellationToken ct);
    Task<CaseDocumentDto> CreateAsync(Guid caseId, CreateCaseDocumentRequest request, CancellationToken ct);
    Task<CaseDocumentDto?> SetProvidedAsync(Guid caseId, Guid documentId, bool isProvided, CancellationToken ct);
    Task<CaseDocumentDto?> UploadFileAsync(Guid caseId, Guid documentId, Stream content, string fileName, string contentType, long fileSizeBytes, CancellationToken ct);
    Task<(StoredFile File, string FileName)?> DownloadFileAsync(Guid caseId, Guid documentId, CancellationToken ct);
}
