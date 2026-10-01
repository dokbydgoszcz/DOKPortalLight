using DokPortal.Domain.Enums;

namespace DokPortal.Application.Supervisions;

public interface ISupervisionService
{
    Task<IReadOnlyList<SupervisionDto>> GetAllAsync(Institution? institution, CancellationToken ct);
    Task<SupervisionDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<SupervisionDto> CreateAsync(CreateSupervisionRequest request, CancellationToken ct);
    Task<SupervisionDto?> UpdateAsync(Guid id, CreateSupervisionRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
}
