using DokPortal.Application.Common;

namespace DokPortal.Application.Missions;

public interface IMissionService
{
    Task<PagedResult<MissionDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct);
    Task<MissionDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<MissionDto> CreateAsync(CreateMissionRequest request, CancellationToken ct);
    Task<MissionDto?> UpdateAsync(Guid id, UpdateMissionRequest request, CancellationToken ct);
}
