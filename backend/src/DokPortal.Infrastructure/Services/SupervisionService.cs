using DokPortal.Application.Attachments;
using DokPortal.Application.Supervisions;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class SupervisionService : ISupervisionService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public SupervisionService(AppDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private IQueryable<Supervision> WithAttendees() =>
        _db.Supervisions.AsNoTracking().Include(s => s.Attendees).ThenInclude(a => a.Person);

    public async Task<IReadOnlyList<SupervisionDto>> GetAllAsync(Institution? institution, CancellationToken ct)
    {
        var q = WithAttendees();
        if (institution.HasValue)
        {
            q = q.Where(s => s.Institution == institution.Value);
        }

        var supervisions = await q.OrderByDescending(s => s.SupervisionDate).ToListAsync(ct);
        var attachments = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.Supervision, supervisions.Select(s => s.Id).ToList(), ct);
        return supervisions.Select(s => ToDto(s, attachments.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<SupervisionDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var supervision = await WithAttendees().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supervision is null) return null;
        return ToDto(supervision, await LoadAttachmentsAsync(id, ct));
    }

    public async Task<IReadOnlyList<SupervisionGroupDto>> GetGroupsAsync(CancellationToken ct)
    {
        var today = _time.Today();
        var rows = await _db.CanonicalMissions.AsNoTracking()
            .Where(m => m.SupervisionGroup != null && m.SupervisionGroup != "" && m.MissionEndDate >= today)
            .Select(m => new { Group = m.SupervisionGroup!, m.PersonId, m.Person!.FirstName, m.Person.LastName })
            .ToListAsync(ct);
        return rows
            .GroupBy(r => r.Group.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(g => new SupervisionGroupDto
            {
                Name = g.Key,
                Members = g.DistinctBy(r => r.PersonId)
                    .OrderBy(r => r.LastName).ThenBy(r => r.FirstName)
                    .Select(r => new SupervisionGroupMemberDto { PersonId = r.PersonId, FullName = $"{r.FirstName} {r.LastName}" })
                    .ToList()
            }).ToList();
    }

    /// <summary>Lista obecności zastępuje poprzednią i wyznacza liczby (obecni / wszyscy na liście).</summary>
    private async Task ApplyAttendanceAsync(Supervision supervision, CreateSupervisionRequest request, CancellationToken ct)
    {
        if (request.Attendance is null)
        {
            if (supervision.Attendees.Count == 0)
            {
                supervision.AttendeesCount = request.AttendeesCount;
                supervision.ExpectedCount = request.ExpectedCount;
            }
            return;
        }

        var items = request.Attendance.GroupBy(a => a.PersonId).Select(g => g.Last()).ToList();
        var ids = items.Select(i => i.PersonId).ToList();
        var known = await _db.People.Where(p => ids.Contains(p.Id)).CountAsync(ct);
        if (known != ids.Count) throw new InvalidOperationException("Na liście obecności są osoby, których nie ma w bazie.");

        _db.SupervisionAttendees.RemoveRange(supervision.Attendees);
        supervision.Attendees.Clear();
        foreach (var item in items)
        {
            var attendee = new SupervisionAttendee { SupervisionId = supervision.Id, PersonId = item.PersonId, IsPresent = item.IsPresent };
            supervision.Attendees.Add(attendee);
        }
        supervision.AttendeesCount = items.Count(i => i.IsPresent);
        supervision.ExpectedCount = items.Count;
    }

    public async Task<SupervisionDto> CreateAsync(CreateSupervisionRequest request, CancellationToken ct)
    {
        var supervision = new Supervision
        {
            Id = Guid.NewGuid(),
            Institution = request.Institution,
            GroupLabel = request.GroupLabel.Trim(),
            SupervisionDate = request.SupervisionDate,
            Topic = request.Topic,
            Conclusion = request.Conclusion,
            CreatedAtUtc = DateTime.UtcNow
        };
        await ApplyAttendanceAsync(supervision, request, ct);
        _db.Supervisions.Add(supervision);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(supervision.Id, ct))!;
    }

    public async Task<SupervisionDto?> UpdateAsync(Guid id, CreateSupervisionRequest request, CancellationToken ct)
    {
        var supervision = await _db.Supervisions.Include(s => s.Attendees).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supervision is null) return null;

        supervision.Institution = request.Institution;
        supervision.GroupLabel = request.GroupLabel.Trim();
        supervision.SupervisionDate = request.SupervisionDate;
        supervision.Topic = request.Topic;
        supervision.Conclusion = request.Conclusion;
        await ApplyAttendanceAsync(supervision, request, ct);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var supervision = await _db.Supervisions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supervision is null) return false;

        supervision.DeletedAtUtc = DateTime.UtcNow;
        supervision.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<IReadOnlyList<AttachmentDto>> LoadAttachmentsAsync(Guid id, CancellationToken ct) =>
        (await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.Supervision, new[] { id }, ct)).GetValueOrDefault(id)
        ?? new List<AttachmentDto>();

    private static SupervisionDto ToDto(Supervision s, IReadOnlyList<AttachmentDto>? attachments = null) => new()
    {
        Id = s.Id,
        Institution = s.Institution,
        GroupLabel = s.GroupLabel,
        SupervisionDate = s.SupervisionDate,
        AttendeesCount = s.AttendeesCount,
        ExpectedCount = s.ExpectedCount,
        Topic = s.Topic,
        Conclusion = s.Conclusion,
        Attachments = attachments ?? Array.Empty<AttachmentDto>(),
        Attendance = s.Attendees
            .Where(a => a.Person is not null)
            .OrderBy(a => a.Person!.LastName).ThenBy(a => a.Person!.FirstName)
            .Select(a => new SupervisionAttendeeDto { PersonId = a.PersonId, FullName = a.Person!.FullName, IsPresent = a.IsPresent })
            .ToList()
    };
}
