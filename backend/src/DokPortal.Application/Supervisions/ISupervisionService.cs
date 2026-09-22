using DokPortal.Domain.Enums;

namespace DokPortal.Application.Supervisions;

public interface ISupervisionService
{
    Task<IReadOnlyList<SupervisionDto>> GetAllAsync(Institution? institution, CancellationToken ct);
    Task<SupervisionDto> CreateAsync(CreateSupervisionRequest request, CancellationToken ct);
}
