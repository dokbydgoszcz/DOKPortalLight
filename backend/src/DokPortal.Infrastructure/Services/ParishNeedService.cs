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

    private IQueryable<ParishNeed> NeedsWithDetails() =>
        _db.ParishNeeds.Include(n => n.Parish).Include(n => n.Assignments).ThenInclude(a => a.Person);

    public async Task<IReadOnlyList<ParishNeedDto>> GetAllAsync(CancellationToken ct)
    {
        var needs = await NeedsWithDetails().AsNoTracking().OrderByDescending(n => n.CreatedAtUtc).ToListAsync(ct);
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

        return ToDto(await NeedsWithDetails().AsNoTracking().FirstAsync(n => n.Id == need.Id, ct));
    }

    public async Task<ParishNeedDto?> UpdateAsync(Guid id, UpdateParishNeedRequest request, CancellationToken ct)
    {
        var need = await _db.ParishNeeds.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (need is null) return null;

        var description = request.Description.Trim();
        if (description.Length == 0) throw new InvalidOperationException("Podaj opis zapotrzebowania.");
        if (!await _db.Parishes.AnyAsync(p => p.Id == request.ParishId, ct)) throw new InvalidOperationException("Nie znaleziono parafii.");

        need.ParishId = request.ParishId;
        need.Description = description;
        await _db.SaveChangesAsync(ct);
        return await LoadAsync(id, ct);
    }

    public async Task<ParishNeedDto?> AssignAsync(Guid id, Guid personId, CancellationToken ct)
    {
        var need = await _db.ParishNeeds.Include(n => n.Assignments).FirstOrDefaultAsync(n => n.Id == id, ct);
        if (need is null) return null;
        if (!await _db.People.AnyAsync(p => p.Id == personId, ct)) throw new InvalidOperationException("Nie znaleziono osoby.");

        if (need.Assignments.All(a => a.PersonId != personId))
        {
            _db.ParishNeedAssignments.Add(new ParishNeedAssignment
            {
                Id = Guid.NewGuid(), ParishNeedId = id, PersonId = personId, AssignedAtUtc = DateTime.UtcNow
            });
        }
        if (need.Status == ParishNeedStatus.Open) need.Status = ParishNeedStatus.Assigned;
        await _db.SaveChangesAsync(ct);
        return await LoadAsync(id, ct);
    }

    public async Task<ParishNeedDto?> UnassignAsync(Guid id, Guid personId, CancellationToken ct)
    {
        var need = await _db.ParishNeeds.Include(n => n.Assignments).FirstOrDefaultAsync(n => n.Id == id, ct);
        if (need is null) return null;

        var assignment = need.Assignments.FirstOrDefault(a => a.PersonId == personId);
        if (assignment is not null)
        {
            _db.ParishNeedAssignments.Remove(assignment);
            if (need.Assignments.Count == 1 && need.Status == ParishNeedStatus.Assigned) need.Status = ParishNeedStatus.Open;
            await _db.SaveChangesAsync(ct);
        }
        return await LoadAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var need = await _db.ParishNeeds.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (need is null) return false;

        need.DeletedAtUtc = DateTime.UtcNow;
        need.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<ParishNeedDto> LoadAsync(Guid id, CancellationToken ct) =>
        ToDto(await NeedsWithDetails().AsNoTracking().FirstAsync(n => n.Id == id, ct));

    private static ParishNeedDto ToDto(ParishNeed n) => new()
    {
        Id = n.Id,
        ParishId = n.ParishId,
        ParishName = n.Parish!.Name,
        Description = n.Description,
        Status = n.Status.ToString(),
        AssignedPeople = n.Assignments
            .OrderBy(a => a.AssignedAtUtc).ThenBy(a => a.Person!.LastName)
            .Select(a => new AssignedPersonDto { PersonId = a.PersonId, FullName = a.Person!.FullName, AssignedAtUtc = a.AssignedAtUtc })
            .ToList()
    };
}
