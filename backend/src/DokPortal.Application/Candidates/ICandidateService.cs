using DokPortal.Application.Common;

namespace DokPortal.Application.Candidates;

public interface ICandidateService
{
    Task<PagedResult<CandidateDto>> SearchAsync(int? year, int page, int pageSize, CancellationToken ct);
    Task<CandidateDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<CandidateDto> CreateAsync(CreateCandidateRequest request, CancellationToken ct);
    Task<CandidateDto?> UpdateAsync(Guid id, UpdateCandidateRequest request, CancellationToken ct);
}
