namespace DokPortal.Application.Documents;

public interface IDocumentService
{
    Task<GeneratedDocumentResult?> GenerateAsync(GenerateDocumentRequest request, string generatedByUserId, CancellationToken ct);
    Task<IReadOnlyList<GeneratedDocumentDto>> GetHistoryAsync(CancellationToken ct);
}
