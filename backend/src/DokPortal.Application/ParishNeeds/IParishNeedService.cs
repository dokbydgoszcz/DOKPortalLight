namespace DokPortal.Application.ParishNeeds;

public interface IParishNeedService
{
    Task<IReadOnlyList<ParishNeedDto>> GetAllAsync(CancellationToken ct);
    Task<ParishNeedDto> CreateAsync(CreateParishNeedRequest request, CancellationToken ct);
    Task<ParishNeedDto?> AssignAsync(Guid id, Guid personId, CancellationToken ct);
}
