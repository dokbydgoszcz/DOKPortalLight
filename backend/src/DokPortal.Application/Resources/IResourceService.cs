namespace DokPortal.Application.Resources;

public interface IResourceService
{
    /// <summary>Wszystkie zasoby alfabetycznie; fraza szuka w tytule i opisie.</summary>
    Task<IReadOnlyList<ResourceDto>> ListAsync(string? query, CancellationToken ct);
    Task<ResourceDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ResourceDto> CreateAsync(CreateResourceRequest request, string createdByUserId, CancellationToken ct);
    Task<ResourceDto?> UpdateAsync(Guid id, UpdateResourceRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
}
