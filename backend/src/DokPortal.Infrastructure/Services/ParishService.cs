using DokPortal.Application.Parishes;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
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
        // Misje skierowane do tej parafii pokazują jej nazwę jako miejsce posługi.
        var missions = await _db.CanonicalMissions.Where(m => m.ParishId == id).ToListAsync(ct);
        foreach (var mission in missions)
        {
            mission.ServicePlace = request.Name;
        }
        await _db.SaveChangesAsync(ct);
        return new ParishDto { Id = parish.Id, Name = parish.Name, City = parish.City };
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var parish = await _db.Parishes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (parish is null) return false;
        await EnsureCanDeleteAsync(id, ct);

        parish.DeletedAtUtc = DateTime.UtcNow;
        parish.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Parafii z osobami, proboszczem, zapotrzebowaniami lub misjami nie usuwamy – zostałyby odwołania do nieistniejącej parafii.</summary>
    private async Task EnsureCanDeleteAsync(Guid id, CancellationToken ct)
    {
        var reasons = new List<string>();
        void Add(int count, string text) { if (count > 0) reasons.Add($"{text} ({count})"); }

        Add(await _db.People.CountAsync(p => p.ParishId == id, ct), "przypisane osoby");
        Add(await _db.PersonFunctions.CountAsync(f => f.Type == FunctionType.Pastor && f.ParishId == id, ct), "proboszcz");
        Add(await _db.ParishNeeds.CountAsync(n => n.ParishId == id, ct), "zapotrzebowania na katechistów");
        Add(await _db.CanonicalMissions.CountAsync(m => m.ParishId == id, ct), "misje kanoniczne");

        if (reasons.Count > 0)
        {
            throw new InvalidOperationException($"Nie można usunąć parafii, bo ma: {string.Join("; ", reasons)}. Najpierw je przenieś lub usuń.");
        }
    }
}
