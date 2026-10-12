using DokPortal.Application.Common;
using DokPortal.Application.Missions;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Domain.Formation;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class MissionService : IMissionService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public MissionService(AppDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<PagedResult<MissionDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct, MissionStatusFilter status = MissionStatusFilter.All)
    {
        var today = _time.Today();
        var q = _db.CanonicalMissions.Include(m => m.Person).AsNoTracking().AsQueryable();
        if (status == MissionStatusFilter.Current) q = q.Where(m => m.MissionEndDate >= today);
        if (status == MissionStatusFilter.Expired) q = q.Where(m => m.MissionEndDate < today);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(m =>
                m.Person!.FirstName.ToLower().Contains(term) ||
                m.Person.LastName.ToLower().Contains(term) ||
                m.ServicePlace.ToLower().Contains(term));
        }

        var total = await q.CountAsync(ct);
        // Ważne misje na górze (najbliższy koniec pierwszy), historia (wygasłe) pod nimi – od najnowszej.
        var entities = await q
            .OrderBy(m => m.MissionEndDate < today ? 1 : 0)
            .ThenBy(m => m.MissionEndDate < today ? DateOnly.MaxValue : m.MissionEndDate)
            .ThenByDescending(m => m.MissionEndDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var attachments = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.Mission, entities.Select(m => m.Id).ToList(), ct);

        return new PagedResult<MissionDto>
        {
            Items = entities.Select(m => ToDto(m, attachments.GetValueOrDefault(m.Id))).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<MissionDto>> GetExpiringAsync(CancellationToken ct)
    {
        var today = _time.Today();
        var horizon = today.AddDays(30);
        var since = today.AddYears(-1);
        var missions = await _db.CanonicalMissions.Include(m => m.Person).AsNoTracking()
            .Where(m => m.MissionEndDate <= horizon && m.MissionEndDate >= since)
            .Where(m => !_db.CanonicalMissions.Any(o => o.PersonId == m.PersonId && o.MissionEndDate > m.MissionEndDate))
            .OrderBy(m => m.MissionEndDate)
            .ToListAsync(ct);
        return missions.Select(m => ToDto(m)).ToList();
    }

    public async Task<MissionDto?> RenewAsync(Guid id, CancellationToken ct)
    {
        var old = await _db.CanonicalMissions.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (old is null) return null;
        if (await _db.CanonicalMissions.AnyAsync(m => m.PersonId == old.PersonId && m.MissionEndDate > old.MissionEndDate, ct))
            throw new InvalidOperationException("Ta osoba ma już nowszą misję – odnów ostatnią.");

        var today = _time.Today();
        var start = old.MissionEndDate.AddDays(1);
        if (start < today) start = today;
        var renewed = new CanonicalMission
        {
            Id = Guid.NewGuid(),
            PersonId = old.PersonId,
            ServicePlace = old.ServicePlace,
            ParishId = old.ParishId,
            SupervisionGroup = old.SupervisionGroup,
            SentToDok = old.SentToDok,
            MissionStartDate = start,
            MissionEndDate = start.AddYears(1),
            GrantedDate = today,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.CanonicalMissions.Add(renewed);
        await CatechistFunction.EnsureAsync(_db, old.PersonId, ct);
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(renewed.Id, ct);
    }

    public async Task<MissionDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var mission = await _db.CanonicalMissions.Include(m => m.Person).AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mission is null) return null;
        var attachments = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.Mission, new[] { id }, ct);
        return ToDto(mission, attachments.GetValueOrDefault(id));
    }

    private static void EnsureDatesAreValid(CreateMissionRequest request)
    {
        if (request.MissionEndDate < request.MissionStartDate)
            throw new InvalidOperationException("Koniec misji nie może być wcześniej niż jej początek.");
    }

    public async Task<MissionDto> CreateAsync(CreateMissionRequest request, CancellationToken ct)
    {
        EnsureDatesAreValid(request);
        var mission = new CanonicalMission
        {
            Id = Guid.NewGuid(),
            PersonId = request.PersonId,
            ServicePlace = request.ServicePlace,
            MissionStartDate = request.MissionStartDate,
            MissionEndDate = request.MissionEndDate,
            GrantedDate = request.GrantedDate,
            SupervisionGroup = request.SupervisionGroup,
            SentToDok = request.SentToDok,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.CanonicalMissions.Add(mission);
        await CatechistFunction.EnsureAsync(_db, request.PersonId, ct);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(mission.Id, ct))!;
    }

    public async Task<MissionDto?> UpdateAsync(Guid id, UpdateMissionRequest request, CancellationToken ct)
    {
        var mission = await _db.CanonicalMissions.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mission is null) return null;
        EnsureDatesAreValid(request);

        var previousPersonId = mission.PersonId;
        // Ręcznie zmienione miejsce posługi to już nie parafia ze skierowania – zmiana jej nazwy nie może go nadpisać.
        if (mission.ServicePlace != request.ServicePlace) mission.ParishId = null;
        mission.PersonId = request.PersonId;
        mission.ServicePlace = request.ServicePlace;
        mission.MissionStartDate = request.MissionStartDate;
        mission.MissionEndDate = request.MissionEndDate;
        mission.GrantedDate = request.GrantedDate;
        mission.SupervisionGroup = request.SupervisionGroup;
        mission.SentToDok = request.SentToDok;
        mission.UpdatedAtUtc = DateTime.UtcNow;

        await CatechistFunction.EnsureAsync(_db, request.PersonId, ct);
        await _db.SaveChangesAsync(ct);
        if (previousPersonId != request.PersonId)
        {
            await CatechistFunction.RemoveIfNoMissionAsync(_db, previousPersonId, ct);
            await _db.SaveChangesAsync(ct);
        }
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var mission = await _db.CanonicalMissions.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mission is null) return false;

        mission.DeletedAtUtc = DateTime.UtcNow;
        mission.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        await CatechistFunction.RemoveIfNoMissionAsync(_db, mission.PersonId, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<PendingCatechistDto>> GetPendingAsync(CancellationToken ct)
    {
        var candidates = await _db.Candidates.Completed()
            .Where(c => !_db.CanonicalMissions.Any(m => m.PersonId == c.PersonId))
            .Include(c => c.Person).ThenInclude(p => p!.Parish)
            .AsNoTracking()
            .ToListAsync(ct);

        return candidates
            .GroupBy(c => c.PersonId)
            .Select(g => g.OrderByDescending(c => c.FormationYearSinceUtc).First())
            .OrderBy(c => c.Person!.LastName).ThenBy(c => c.Person!.FirstName)
            .Select(c => new PendingCatechistDto
            {
                CandidateId = c.Id,
                PersonId = c.PersonId,
                PersonFullName = c.Person!.FullName,
                ParishName = c.Person.Parish?.Name,
                FormationCompletedOn = DateOnly.FromDateTime(c.FormationYearSinceUtc)
            })
            .ToList();
    }

    public async Task<MissionDto> GrantAsync(Guid personId, CancellationToken ct)
    {
        var pending = await GetPendingAsync(ct);
        if (pending.All(p => p.PersonId != personId))
        {
            throw new InvalidOperationException("Ta osoba nie czeka na udzielenie posługi.");
        }

        var today = _time.Today();
        return await CreateAsync(new CreateMissionRequest
        {
            PersonId = personId,
            ServicePlace = "",
            MissionStartDate = today,
            MissionEndDate = today.AddYears(1),
            GrantedDate = today
        }, ct);
    }

    private MissionDto ToDto(CanonicalMission m, IReadOnlyList<DokPortal.Application.Attachments.AttachmentDto>? attachments = null) => new()
    {
        Id = m.Id,
        PersonId = m.PersonId,
        PersonFullName = m.Person!.FullName,
        ServicePlace = m.ServicePlace,
        MissionStartDate = m.MissionStartDate,
        MissionEndDate = m.MissionEndDate,
        GrantedDate = m.GrantedDate,
        SupervisionGroup = m.SupervisionGroup,
        SentToDok = m.SentToDok,
        Status = ComputeStatus(m.MissionEndDate),
        Attachments = attachments ?? Array.Empty<DokPortal.Application.Attachments.AttachmentDto>()
    };

    private string ComputeStatus(DateOnly endDate)
    {
        var today = _time.Today();
        if (endDate < today) return "wygasła";
        if (endDate <= today.AddDays(30)) return "wygasa";
        return "ważna";
    }
}
