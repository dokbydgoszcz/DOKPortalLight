namespace DokPortal.Application.Parishes;

public interface IParishService
{
    Task<IReadOnlyList<ParishDto>> GetAllAsync(CancellationToken ct);
    Task<ParishDto> CreateAsync(CreateParishRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
}
