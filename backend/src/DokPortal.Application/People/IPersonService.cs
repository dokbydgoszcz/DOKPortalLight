using DokPortal.Application.Common;
using DokPortal.Domain.Enums;

namespace DokPortal.Application.People;

public interface IPersonService
{
    Task<PagedResult<PersonDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct, FunctionType? function = null);
    Task<PersonDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<PersonDto> CreateAsync(CreatePersonRequest request, CancellationToken ct);
    Task<PersonDto?> UpdateAsync(Guid id, UpdatePersonRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
}
