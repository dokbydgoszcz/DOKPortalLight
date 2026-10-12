namespace DokPortal.Application.PastoralNotes;

public enum PastoralNoteAccess
{
    NotFound,
    Hidden,
    Visible
}

public interface IPastoralNoteService
{
    Task<IReadOnlyList<PastoralNoteDto>> GetVisibleForCaseAsync(Guid caseId, string currentUserId, bool isPrivileged, CancellationToken ct);
    Task<PastoralNoteDto> CreateAsync(Guid caseId, string authorUserId, CreatePastoralNoteRequest request, CancellationToken ct);
    /// <summary>Czy notatka istnieje w tej sprawie i czy dany użytkownik może ją widzieć (autor albo uprawniony do wszystkich notatek).</summary>
    Task<PastoralNoteAccess> GetAccessAsync(Guid caseId, Guid noteId, string currentUserId, bool isPrivileged, CancellationToken ct);
    Task<bool> HasNotesFromOthersAsync(Guid caseId, string currentUserId, CancellationToken ct);
    /// <summary>Poprawia treść notatki; tylko autor (dla innych notatka zachowuje się jak nieistniejąca – null).</summary>
    Task<PastoralNoteDto?> UpdateAsync(Guid caseId, Guid noteId, string currentUserId, CreatePastoralNoteRequest request, CancellationToken ct);
    /// <summary>Usuwa notatkę na stałe (sprostowanie/usunięcie danych); tylko autor. Załączniki usuwa wywołujący.</summary>
    Task<bool> DeleteAsync(Guid caseId, Guid noteId, string currentUserId, CancellationToken ct);
}
