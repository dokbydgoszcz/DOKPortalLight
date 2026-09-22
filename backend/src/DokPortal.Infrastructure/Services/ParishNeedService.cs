using DokPortal.Application.ParishNeeds;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class ParishNeedService : IParishNeedService
{
    private readonly AppDbContext _db;

    public ParishNeedService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ParishNeedDto>> GetAllAsync(CancellationToken ct)
    {
        var needs = await _db.ParishNeeds
            .Include(n => n.Parish)
            .Include(n => n.AssignedPerson)
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(ct);
        return needs.Select(ToDto).ToList();
    }

    public async Task<ParishNeedDto> CreateAsync(CreateParishNeedRequest request, CancellationToken ct)
    {
        var need = new ParishNeed
        {
            Id = Guid.NewGuid(),
            ParishId = request.ParishId,
            Description = request.Description,
            Status = ParishNeedStatus.Open,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.ParishNeeds.Add(need);
        await _db.SaveChangesAsync(ct);

        var saved = await _db.ParishNeeds.Include(n => n.Parish).AsNoTracking().FirstAsync(n => n.Id == need.Id, ct);
        return ToDto(saved);
    }

    public async Task<ParishNeedDto?> AssignAsync(Guid id, Guid personId, CancellationToken ct)
    {
        var need = await _db.ParishNeeds.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (need is null) return null;

        need.AssignedPersonId = personId;
        need.AssignedAtUtc = DateTime.UtcNow;
        need.Status = ParishNeedStatus.Assigned;
        await _db.SaveChangesAsync(ct);

        var saved = await _db.ParishNeeds
            .Include(n => n.Parish)
            .Include(n => n.AssignedPerson)
            .AsNoTracking()
            .FirstAsync(n => n.Id == id, ct);
        return ToDto(saved);
    }

    private static ParishNeedDto ToDto(ParishNeed n) => new()
    {
        Id = n.Id,
        ParishId = n.ParishId,
        ParishName = n.Parish!.Name,
        Description = n.Description,
        Status = n.Status.ToString(),
        AssignedPersonId = n.AssignedPersonId,
        AssignedPersonName = n.AssignedPerson?.FullName,
        AssignedAtUtc = n.AssignedAtUtc
    };
}
