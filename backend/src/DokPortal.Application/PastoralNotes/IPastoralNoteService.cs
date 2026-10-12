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
    /// <summary>Czy użytkownik ma notatki w sprawie (autor może udostępniać je także po przekazaniu sprawy).</summary>
    Task<bool> HasOwnNotesAsync(Guid caseId, string currentUserId, CancellationToken ct);
    /// <summary>Sprawy z notatkami użytkownika, które prowadzi już inny katechista.</summary>
    Task<IReadOnlyList<HandedOverCaseDto>> GetHandedOverAsync(string currentUserId, CancellationToken ct);
    Task<NoteSharingDto> GetSharingAsync(Guid caseId, string currentUserId, CancellationToken ct);
    /// <summary>Udostępnia wszystkie notatki użytkownika w sprawie jej obecnemu katechiście prowadzącemu.</summary>
    /// <exception cref="InvalidOperationException">Brak własnych notatek albo użytkownik sam prowadzi sprawę.</exception>
    Task<NoteSharingDto> ShareWithCurrentCatechistAsync(Guid caseId, string currentUserId, CancellationToken ct);
    Task<NoteSharingDto> RevokeShareAsync(Guid caseId, string currentUserId, Guid personId, CancellationToken ct);
}
