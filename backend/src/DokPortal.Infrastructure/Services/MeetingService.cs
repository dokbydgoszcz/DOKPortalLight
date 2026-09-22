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
