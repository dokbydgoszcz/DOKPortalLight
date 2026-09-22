namespace DokPortal.Application.Parishes;

public interface IParishService
{
    Task<IReadOnlyList<ParishDto>> GetAllAsync(CancellationToken ct);
    Task<ParishDto> CreateAsync(CreateParishRequest request, CancellationToken ct);
}
