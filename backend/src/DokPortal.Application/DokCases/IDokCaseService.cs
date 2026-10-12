using DokPortal.Application.Common;
using DokPortal.Domain.Enums;

namespace DokPortal.Application.DokCases;

public interface IDokCaseService
{
    Task<PagedResult<DokCaseDto>> SearchAsync(DokPath? path, int page, int pageSize, CancellationToken ct, DokStage? stage = null);
    Task<PagedResult<DokCaseDto>> SearchGraduatesAsync(string? query, int page, int pageSize, CancellationToken ct);
    Task<DokCaseDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<DokCaseDto> CreateAsync(CreateDokCaseRequest request, CancellationToken ct);
    /// <summary>Zmienia etap, ścieżkę, katechistę i mentora (podopiecznego zmienić nie można); zmiany trafiają do historii.</summary>
    /// <exception cref="InvalidOperationException">Inny podopieczny, nieistniejący katechista/mentor albo etap spoza ścieżki.</exception>
    Task<DokCaseDto?> UpdateAsync(Guid id, UpdateDokCaseRequest request, CancellationToken ct, DokCaseActor? actor = null);
    /// <summary>Historia zmian sprawy, najnowsze pierwsze; null, gdy sprawa nie istnieje lub jest poza zakresem użytkownika.</summary>
    Task<IReadOnlyList<DokCaseEventDto>?> GetHistoryAsync(Guid id, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
}
