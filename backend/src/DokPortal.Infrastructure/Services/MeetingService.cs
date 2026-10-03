using DokPortal.Application.DokCases;
using DokPortal.Application.Meetings;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class MeetingService : IMeetingService
{
    private readonly AppDbContext _db;
    private readonly ICaseScopeProvider _scope;

    public MeetingService(AppDbContext db, ICaseScopeProvider? scope = null)
    {
        _db = db;
        _scope = scope ?? new AllCasesScopeProvider();
    }

    private async Task<IQueryable<Meeting>> VisibleAsync(CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        return _db.Meetings.ForScope(_db, scope);
    }

    private static IQueryable<Meeting> WithDetails(IQueryable<Meeting> meetings) =>
        meetings
            .Include(m => m.DokCase).ThenInclude(c => c!.Person)
            .Include(m => m.Attendees).ThenInclude(a => a.DokCase).ThenInclude(c => c!.Person);

    /// <summary>
    /// Spotkanie jest albo indywidualne (jedna sprawa), albo grupowe (lista uczestników). Wszystkie wskazane
    /// sprawy muszą istnieć i należeć do zakresu użytkownika.
    /// </summary>
    private async Task ValidateAsync(CreateMeetingRequest request, CaseScope scope, CancellationToken ct)
    {
        var attendeeIds = (request.Attendees ?? Array.Empty<AttendeeRequest>()).Select(a => a.DokCaseId).ToList();

        if (request.DokCaseId is not null && attendeeIds.Count > 0)
        {
            throw new InvalidOperationException("Spotkanie może być indywidualne albo grupowe, nie oba naraz.");
        }
        if (attendeeIds.Distinct().Count() != attendeeIds.Count)
        {
            throw new InvalidOperationException("Ten sam podopieczny występuje na liście uczestników więcej niż raz.");
        }

        var wanted = request.DokCaseId is { } caseId ? attendeeIds.Append(caseId).ToList() : attendeeIds;
        if (wanted.Count == 0) return;

        var accessible = await _db.DokCases.ForScope(scope).CountAsync(c => wanted.Contains(c.Id), ct);
        if (accessible != wanted.Count)
        {
            throw new InvalidOperationException("Nie masz dostępu do wskazanej sprawy DOK.");
        }
    }

    public async Task<IReadOnlyList<MeetingDto>> GetAllAsync(CancellationToken ct)
    {
        var meetings = await WithDetails(await VisibleAsync(ct)).AsNoTracking()
            .OrderByDescending(m => m.MeetingDate)
            .ToListAsync(ct);
        return meetings.Select(ToDto).ToList();
    }

    public async Task<MeetingDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var meeting = await WithDetails(await VisibleAsync(ct)).AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        return meeting is null ? null : ToDto(meeting);
    }

    public async Task<MeetingDto> CreateAsync(CreateMeetingRequest request, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        await ValidateAsync(request, scope, ct);

        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            DokCaseId = request.DokCaseId,
            GroupLabel = request.GroupLabel,
            CatechistPersonId = request.DokCaseId is null ? scope.PersonId : null,
            MeetingDate = request.MeetingDate,
            IsAttended = request.IsAttended,
            Notes = request.Notes,
            CreatedAtUtc = DateTime.UtcNow,
            Attendees = (request.Attendees ?? Array.Empty<AttendeeRequest>())
                .Select(a => new MeetingAttendee { Id = Guid.NewGuid(), DokCaseId = a.DokCaseId, IsAttended = a.IsAttended })
                .ToList()
        };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync(ct);

        var saved = await WithDetails(_db.Meetings).AsNoTracking().FirstAsync(m => m.Id == meeting.Id, ct);
        return ToDto(saved);
    }

    public async Task<MeetingDto?> UpdateAsync(Guid id, CreateMeetingRequest request, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var meeting = await (await VisibleAsync(ct)).Include(m => m.Attendees).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return null;
        await ValidateAsync(request, scope, ct);

        meeting.DokCaseId = request.DokCaseId;
        meeting.GroupLabel = request.GroupLabel;
        meeting.CatechistPersonId = request.DokCaseId is null ? meeting.CatechistPersonId ?? scope.PersonId : null;
        meeting.MeetingDate = request.MeetingDate;
        meeting.IsAttended = request.IsAttended;
        meeting.Notes = request.Notes;

        var wanted = (request.Attendees ?? Array.Empty<AttendeeRequest>()).ToDictionary(a => a.DokCaseId);
        foreach (var existing in meeting.Attendees.ToList())
        {
            if (wanted.Remove(existing.DokCaseId, out var kept))
            {
                existing.IsAttended = kept.IsAttended;
            }
            else
            {
                _db.MeetingAttendees.Remove(existing);
            }
        }
        foreach (var added in wanted.Values)
        {
            meeting.Attendees.Add(new MeetingAttendee { Id = Guid.NewGuid(), DokCaseId = added.DokCaseId, IsAttended = added.IsAttended });
        }
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<MeetingDto?> SetAttendanceAsync(Guid id, bool? isAttended, CancellationToken ct)
    {
        var meeting = await (await VisibleAsync(ct)).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return null;

        meeting.IsAttended = isAttended;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<MeetingDto?> SetAttendeeAttendanceAsync(Guid meetingId, Guid dokCaseId, bool? isAttended, CancellationToken ct)
    {
        var meeting = await (await VisibleAsync(ct)).Include(m => m.Attendees).FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        var attendee = meeting?.Attendees.FirstOrDefault(a => a.DokCaseId == dokCaseId);
        if (attendee is null) return null;

        attendee.IsAttended = isAttended;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(meetingId, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var meeting = await (await VisibleAsync(ct)).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return false;

        meeting.DeletedAtUtc = DateTime.UtcNow;
        meeting.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static MeetingDto ToDto(Meeting m) => new()
    {
        Id = m.Id,
        DokCaseId = m.DokCaseId,
        CaseLabel = m.DokCase?.Person?.FullName,
        GroupLabel = m.GroupLabel,
        MeetingDate = m.MeetingDate,
        IsAttended = m.IsAttended,
        Notes = m.Notes,
        Attendees = m.Attendees
            .Select(a => new MeetingAttendeeDto
            {
                DokCaseId = a.DokCaseId,
                PersonFullName = a.DokCase?.Person?.FullName ?? string.Empty,
                IsAttended = a.IsAttended
            })
            .OrderBy(a => a.PersonFullName)
            .ToList()
    };
}
