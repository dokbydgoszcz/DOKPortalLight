using DokPortal.Application.Common;

namespace DokPortal.Application.People;

public interface IPersonService
{
    Task<PagedResult<PersonDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct);
    Task<PersonDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<PersonDto> CreateAsync(CreatePersonRequest request, CancellationToken ct);
    Task<PersonDto?> UpdateAsync(Guid id, UpdatePersonRequest request, CancellationToken ct);
}
