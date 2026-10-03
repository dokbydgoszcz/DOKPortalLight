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
    Task<ParishNeedDto?> UnassignAsync(Guid id, Guid personId, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
}
