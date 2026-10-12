namespace DokPortal.Application.ParishNeeds;

public interface IParishNeedService
{
    Task<IReadOnlyList<ParishNeedDto>> GetAllAsync(CancellationToken ct);
    Task<ParishNeedDto> CreateAsync(CreateParishNeedRequest request, CancellationToken ct);
    /// <exception cref="InvalidOperationException">Pusty opis albo nieistniejąca parafia.</exception>
    Task<ParishNeedDto?> UpdateAsync(Guid id, UpdateParishNeedRequest request, CancellationToken ct);
    /// <summary>Dodaje osobę do skierowanych (kolejna osoba nie zastępuje poprzednich; powtórzenie nic nie zmienia).</summary>
    /// <exception cref="InvalidOperationException">Nieistniejąca osoba.</exception>
    Task<ParishNeedDto?> AssignAsync(Guid id, Guid personId, CancellationToken ct);
    /// <summary>Odpina osobę; cofa misję założoną przez skierowanie (albo wpisaną w niej parafię) i – bez misji – funkcję Katechista.</summary>
    Task<ParishNeedDto?> UnassignAsync(Guid id, Guid personId, CancellationToken ct);
    /// <summary>Zamyka zapotrzebowanie (bez kolejnych skierowań) albo otwiera je ponownie.</summary>
    Task<ParishNeedDto?> SetClosedAsync(Guid id, bool closed, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
}
