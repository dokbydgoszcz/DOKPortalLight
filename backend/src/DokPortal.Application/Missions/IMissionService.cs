using DokPortal.Application.Common;

namespace DokPortal.Application.Missions;

public interface IMissionService
{
    Task<PagedResult<MissionDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct);
    Task<MissionDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<MissionDto> CreateAsync(CreateMissionRequest request, CancellationToken ct);
    Task<MissionDto?> UpdateAsync(Guid id, UpdateMissionRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
    /// <summary>Osoby, które ukończyły formację w SKŚP i nie mają jeszcze misji (status „przed udzieleniem posługi”).</summary>
    Task<IReadOnlyList<PendingCatechistDto>> GetPendingAsync(CancellationToken ct);
    /// <summary>Jednym ruchem udziela posłania: zakłada misję z datą dzisiejszą (miejsce i daty można poprawić w edycji).</summary>
    /// <exception cref="InvalidOperationException">Osoba nie czeka na udzielenie posługi.</exception>
    Task<MissionDto> GrantAsync(Guid personId, CancellationToken ct);
}
