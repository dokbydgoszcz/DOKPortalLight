using DokPortal.Application.Parishes;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class ParishService : IParishService
{
    private readonly AppDbContext _db;

    public ParishService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ParishDto>> GetAllAsync(CancellationToken ct)
    {
        var parishes = await _db.Parishes.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        return parishes.Select(p => new ParishDto { Id = p.Id, Name = p.Name, City = p.City }).ToList();
    }

    public async Task<ParishDto> CreateAsync(CreateParishRequest request, CancellationToken ct)
    {
        var parish = new Parish { Id = Guid.NewGuid(), Name = request.Name, City = request.City };
        _db.Parishes.Add(parish);
        await _db.SaveChangesAsync(ct);
        return new ParishDto { Id = parish.Id, Name = parish.Name, City = parish.City };
    }

    public async Task<ParishDto?> UpdateAsync(Guid id, UpdateParishRequest request, CancellationToken ct)
    {
        var parish = await _db.Parishes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (parish is null) return null;

        parish.Name = request.Name;
        parish.City = request.City;
        await _db.SaveChangesAsync(ct);
        return new ParishDto { Id = parish.Id, Name = parish.Name, City = parish.City };
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var parish = await _db.Parishes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (parish is null) return false;

        parish.DeletedAtUtc = DateTime.UtcNow;
        parish.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
