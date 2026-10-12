using DokPortal.Application.Common;

namespace DokPortal.Application.Missions;

public interface IMissionService
{
    /// <summary>Najpierw misje ważne (najbliższy koniec pierwszy), potem wygasłe (najnowsze pierwsze).</summary>
    Task<PagedResult<MissionDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct, MissionStatusFilter status = MissionStatusFilter.All);
    /// <summary>Misje do odnowienia: kończące się w ciągu 30 dni albo wygasłe w ostatnim roku, bez późniejszej misji tej osoby.</summary>
    Task<IReadOnlyList<MissionDto>> GetExpiringAsync(CancellationToken ct);
    /// <summary>Zakłada kolejną misję na rok (od dnia po końcu starej, najwcześniej dziś), z tym samym miejscem i grupą.</summary>
    /// <exception cref="InvalidOperationException">Osoba ma już nowszą misję.</exception>
    Task<MissionDto?> RenewAsync(Guid id, CancellationToken ct);
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
