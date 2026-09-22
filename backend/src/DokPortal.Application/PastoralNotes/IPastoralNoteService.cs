namespace DokPortal.Application.PastoralNotes;

public interface IPastoralNoteService
{
    Task<IReadOnlyList<PastoralNoteDto>> GetVisibleForCaseAsync(Guid caseId, string currentUserId, bool isPrivileged, CancellationToken ct);
    Task<PastoralNoteDto> CreateAsync(Guid caseId, string authorUserId, CreatePastoralNoteRequest request, CancellationToken ct);
    Task<bool> HasNotesFromOthersAsync(Guid caseId, string currentUserId, CancellationToken ct);
}
