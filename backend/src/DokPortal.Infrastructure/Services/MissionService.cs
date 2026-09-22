using DokPortal.Application.Common;
using DokPortal.Application.Missions;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class MissionService : IMissionService
{
    private readonly AppDbContext _db;

    public MissionService(AppDbContext db) => _db = db;

    public async Task<PagedResult<MissionDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.CanonicalMissions.Include(m => m.Person).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(m =>
                m.Person!.FirstName.ToLower().Contains(term) ||
                m.Person.LastName.ToLower().Contains(term) ||
                m.ServicePlace.ToLower().Contains(term));
        }

        var total = await q.CountAsync(ct);
        var entities = await q
            .OrderBy(m => m.MissionEndDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<MissionDto>
        {
            Items = entities.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<MissionDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var mission = await _db.CanonicalMissions.Include(m => m.Person).AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        return mission is null ? null : ToDto(mission);
    }

    public async Task<MissionDto> CreateAsync(CreateMissionRequest request, CancellationToken ct)
    {
        var mission = new CanonicalMission
        {
            Id = Guid.NewGuid(),
            PersonId = request.PersonId,
            ServicePlace = request.ServicePlace,
            MissionStartDate = request.MissionStartDate,
            MissionEndDate = request.MissionEndDate,
            GrantedDate = request.GrantedDate,
            GrantedPlace = request.GrantedPlace,
            SupervisionGroup = request.SupervisionGroup,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.CanonicalMissions.Add(mission);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(mission.Id, ct))!;
    }

    public async Task<MissionDto?> UpdateAsync(Guid id, UpdateMissionRequest request, CancellationToken ct)
    {
        var mission = await _db.CanonicalMissions.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mission is null) return null;

        mission.PersonId = request.PersonId;
        mission.ServicePlace = request.ServicePlace;
        mission.MissionStartDate = request.MissionStartDate;
        mission.MissionEndDate = request.MissionEndDate;
        mission.GrantedDate = request.GrantedDate;
        mission.GrantedPlace = request.GrantedPlace;
        mission.SupervisionGroup = request.SupervisionGroup;
        mission.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    private static MissionDto ToDto(CanonicalMission m) => new()
    {
        Id = m.Id,
        PersonId = m.PersonId,
        PersonFullName = m.Person!.FullName,
        ServicePlace = m.ServicePlace,
        MissionStartDate = m.MissionStartDate,
        MissionEndDate = m.MissionEndDate,
        GrantedDate = m.GrantedDate,
        GrantedPlace = m.GrantedPlace,
        SupervisionGroup = m.SupervisionGroup,
        Status = ComputeStatus(m.MissionEndDate)
    };

    private static string ComputeStatus(DateOnly endDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (endDate < today) return "wygasła";
        if (endDate <= today.AddDays(30)) return "wygasa";
        return "ważna";
    }
}
