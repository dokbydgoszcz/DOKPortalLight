using DokPortal.Application.Attachments;
using DokPortal.Application.Resources;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class ResourceService : IResourceService
{
    private readonly AppDbContext _db;

    public ResourceService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ResourceDto>> ListAsync(string? query, CancellationToken ct)
    {
        var q = _db.LibraryResources.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(r => r.Title.ToLower().Contains(term) || (r.Description != null && r.Description.ToLower().Contains(term)));
        }

        var resources = await q.OrderBy(r => r.Title).ToListAsync(ct);
        var files = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.Resource, resources.Select(r => r.Id).ToList(), ct);
        return resources.Select(r => ToDto(r, files)).ToList();
    }

    public async Task<ResourceDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var resource = await _db.LibraryResources.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (resource is null) return null;
        var files = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.Resource, new[] { id }, ct);
        return ToDto(resource, files);
    }

    public async Task<ResourceDto> CreateAsync(CreateResourceRequest request, string createdByUserId, CancellationToken ct)
    {
        var resource = new LibraryResource
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.LibraryResources.Add(resource);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(resource.Id, ct))!;
    }

    public async Task<ResourceDto?> UpdateAsync(Guid id, UpdateResourceRequest request, CancellationToken ct)
    {
        var resource = await _db.LibraryResources.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (resource is null) return null;

        resource.Title = request.Title.Trim();
        resource.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        resource.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var resource = await _db.LibraryResources.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (resource is null) return false;

        resource.DeletedAtUtc = DateTime.UtcNow;
        resource.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static ResourceDto ToDto(LibraryResource r, Dictionary<Guid, List<AttachmentDto>> files) => new()
    {
        Id = r.Id,
        Title = r.Title,
        Description = r.Description,
        CreatedAtUtc = r.CreatedAtUtc,
        Files = files.GetValueOrDefault(r.Id) ?? new List<AttachmentDto>()
    };
}
