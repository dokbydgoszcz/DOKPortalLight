namespace DokPortal.Application.Formators;

public interface IFormatorService
{
    Task<IReadOnlyList<FormatorDto>> GetAllAsync(CancellationToken ct);
    Task<FormatorDto> CreateAsync(CreateFormatorRequest request, CancellationToken ct);
}
