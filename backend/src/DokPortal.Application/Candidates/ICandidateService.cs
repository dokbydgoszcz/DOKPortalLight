using DokPortal.Application.Common;

namespace DokPortal.Application.Candidates;

public interface ICandidateService
{
    Task<PagedResult<CandidateDto>> SearchAsync(int? year, int page, int pageSize, CancellationToken ct);
    Task<CandidateDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<CandidateDto> CreateAsync(CreateCandidateRequest request, CancellationToken ct, CandidateActor? actor = null);
    Task<CandidateDto?> UpdateAsync(Guid id, UpdateCandidateRequest request, CancellationToken ct, CandidateActor? actor = null);
    /// <summary>Przenosi zaznaczonych kandydatów o rok dalej (z III roku: kończy formację). Zatrzymanych i ukończonych pomija z powodem.</summary>
    /// <exception cref="InvalidOperationException">Pusta lista albo więcej niż 500 kandydatów.</exception>
    Task<AdvanceCandidatesResultDto> AdvanceAsync(IReadOnlyCollection<Guid> candidateIds, CandidateActor actor, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
}
