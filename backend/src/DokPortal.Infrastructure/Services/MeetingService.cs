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

    /// <summary>
    /// Zmieniać i usuwać spotkanie może osoba z pełnym zakresem, prowadzący zajęcia grupowe albo katechista sprawy
    /// (spotkanie indywidualne). Zajęcia „udostępnione” (bo uczestniczy w nich mój podopieczny) są tylko do podglądu i obecności.
    /// </summary>
    private async Task EnsureCanManageAsync(Meeting meeting, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        if (scope.ViewAll) return;
        if (meeting.DokCaseId is { } caseId && await _db.DokCases.ForScope(scope).AnyAsync(c => c.Id == caseId, ct)) return;
        if (meeting.DokCaseId is null && scope.PersonId is { } personId && meeting.CatechistPersonId == personId) return;
        throw new InvalidOperationException("Te zajęcia prowadzi inna osoba – możesz tylko zaznaczyć obecność swojego podopiecznego.");
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
        if (request.EndTime is not null && request.StartTime is null)
            throw new InvalidOperationException("Podaj godzinę rozpoczęcia, skoro jest godzina zakończenia.");
        if (request.StartTime is { } start && request.EndTime is { } end && end <= start)
            throw new InvalidOperationException("Godzina zakończenia musi być późniejsza niż rozpoczęcia.");

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
        return meetings.OrderByDescending(m => m.MeetingDate).ThenBy(m => m.StartTime).Select(ToDto).ToList();
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

        var dates = new List<DateOnly> { request.MeetingDate };
        if (request.RepeatWeeklyUntil is { } until)
        {
            if (until <= request.MeetingDate)
                throw new InvalidOperationException("Data końca serii musi być późniejsza niż data pierwszego spotkania.");
            if (until > request.MeetingDate.AddYears(1))
                throw new InvalidOperationException("Seria spotkań może trwać najwyżej rok.");
            for (var d = request.MeetingDate.AddDays(7); d <= until; d = d.AddDays(7)) dates.Add(d);
        }
        Guid? seriesId = dates.Count > 1 ? Guid.NewGuid() : null;

        var meetings = dates.Select(date => NewMeeting(request, scope, date, seriesId)).ToList();
        _db.Meetings.AddRange(meetings);
        await _db.SaveChangesAsync(ct);

        var saved = await WithDetails(_db.Meetings).AsNoTracking().FirstAsync(m => m.Id == meetings[0].Id, ct);
        return ToDto(saved);
    }

    private static Meeting NewMeeting(CreateMeetingRequest request, CaseScope scope, DateOnly date, Guid? seriesId) =>
        new()
        {
            Id = Guid.NewGuid(),
            SeriesId = seriesId,
            DokCaseId = request.DokCaseId,
            GroupLabel = request.GroupLabel,
            CatechistPersonId = request.DokCaseId is null ? scope.PersonId : null,
            MeetingDate = date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            // Obecność wpisana w formularzu dotyczy tylko pierwszego spotkania serii.
            IsAttended = date == request.MeetingDate ? request.IsAttended : null,
            Notes = request.Notes,
            CreatedAtUtc = DateTime.UtcNow,
            Attendees = (request.Attendees ?? Array.Empty<AttendeeRequest>())
                .Select(a => new MeetingAttendee { Id = Guid.NewGuid(), DokCaseId = a.DokCaseId, IsAttended = date == request.MeetingDate ? a.IsAttended : null })
                .ToList()
        };

    public async Task<MeetingDto?> UpdateAsync(Guid id, CreateMeetingRequest request, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var meeting = await (await VisibleAsync(ct)).Include(m => m.Attendees).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return null;
        await EnsureCanManageAsync(meeting, ct);
        await ValidateAsync(request, scope, ct);

        meeting.DokCaseId = request.DokCaseId;
        meeting.GroupLabel = request.GroupLabel;
        meeting.CatechistPersonId = request.DokCaseId is null ? meeting.CatechistPersonId ?? scope.PersonId : null;
        if (meeting.MeetingDate != request.MeetingDate || meeting.StartTime != request.StartTime)
        {
            // Przesunięte spotkanie dostaje przypomnienie na nowy termin.
            meeting.ReminderSentAtUtc = null;
        }
        meeting.MeetingDate = request.MeetingDate;
        meeting.StartTime = request.StartTime;
        meeting.EndTime = request.EndTime;
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
        // W cudzych zajęciach katechista zaznacza obecność tylko swoich podopiecznych.
        var scope = await _scope.GetAsync(ct);
        var ownsMeeting = scope.ViewAll || (scope.PersonId is { } owner && meeting!.CatechistPersonId == owner);
        if (!ownsMeeting && !await _db.DokCases.ForScope(scope).AnyAsync(c => c.Id == dokCaseId, ct)) return null;

        attendee.IsAttended = isAttended;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(meetingId, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct, bool withFollowing = false)
    {
        var visible = await VisibleAsync(ct);
        var meeting = await visible.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return false;
        await EnsureCanManageAsync(meeting, ct);

        var toDelete = new List<Meeting> { meeting };
        if (withFollowing && meeting.SeriesId is { } seriesId)
        {
            toDelete.AddRange(await visible
                .Where(m => m.SeriesId == seriesId && m.Id != meeting.Id && m.MeetingDate > meeting.MeetingDate)
                .ToListAsync(ct));
        }
        var now = DateTime.UtcNow;
        foreach (var m in toDelete)
        {
            m.DeletedAtUtc = now;
            m.DeletedBy = deletedBy;
        }
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
        StartTime = m.StartTime,
        EndTime = m.EndTime,
        IsAttended = m.IsAttended,
        Notes = m.Notes,
        SeriesId = m.SeriesId,
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
