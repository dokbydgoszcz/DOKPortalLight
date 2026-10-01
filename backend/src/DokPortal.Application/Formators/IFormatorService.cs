namespace DokPortal.Application.Formators;

public interface IFormatorService
{
    Task<IReadOnlyList<FormatorDto>> GetAllAsync(CancellationToken ct);
    Task<FormatorDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<FormatorDto> CreateAsync(CreateFormatorRequest request, CancellationToken ct);
    Task<FormatorDto?> UpdateAsync(Guid id, CreateFormatorRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
}
