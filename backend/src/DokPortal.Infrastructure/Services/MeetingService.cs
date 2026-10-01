using DokPortal.Application.Meetings;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class MeetingService : IMeetingService
{
    private readonly AppDbContext _db;

    public MeetingService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<MeetingDto>> GetAllAsync(CancellationToken ct)
    {
        var meetings = await _db.Meetings.Include(m => m.DokCase).ThenInclude(c => c!.Person).AsNoTracking()
            .OrderByDescending(m => m.MeetingDate)
            .ToListAsync(ct);
        return meetings.Select(ToDto).ToList();
    }

    public async Task<MeetingDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var meeting = await _db.Meetings.Include(m => m.DokCase).ThenInclude(c => c!.Person).AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        return meeting is null ? null : ToDto(meeting);
    }

    public async Task<MeetingDto> CreateAsync(CreateMeetingRequest request, CancellationToken ct)
    {
        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            DokCaseId = request.DokCaseId,
            GroupLabel = request.GroupLabel,
            MeetingDate = request.MeetingDate,
            IsAttended = request.IsAttended,
            Notes = request.Notes,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync(ct);

        var saved = await _db.Meetings.Include(m => m.DokCase).ThenInclude(c => c!.Person).AsNoTracking()
            .FirstAsync(m => m.Id == meeting.Id, ct);
        return ToDto(saved);
    }

    public async Task<MeetingDto?> UpdateAsync(Guid id, CreateMeetingRequest request, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (meeting is null) return null;

        meeting.DokCaseId = request.DokCaseId;
        meeting.GroupLabel = request.GroupLabel;
        meeting.MeetingDate = request.MeetingDate;
        meeting.IsAttended = request.IsAttended;
        meeting.Notes = request.Notes;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == id, ct);
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
        Notes = m.Notes
    };
}
