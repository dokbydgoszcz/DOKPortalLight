using DokPortal.Application.Common;
using DokPortal.Domain.Enums;

namespace DokPortal.Application.DokCases;

public interface IDokCaseService
{
    Task<PagedResult<DokCaseDto>> SearchAsync(DokPath? path, int page, int pageSize, CancellationToken ct);
    Task<DokCaseDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<DokCaseDto> CreateAsync(CreateDokCaseRequest request, CancellationToken ct);
    Task<DokCaseDto?> UpdateAsync(Guid id, UpdateDokCaseRequest request, CancellationToken ct);
}
