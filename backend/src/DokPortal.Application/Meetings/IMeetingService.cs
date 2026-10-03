namespace DokPortal.Application.Meetings;

public interface IMeetingService
{
    Task<IReadOnlyList<MeetingDto>> GetAllAsync(CancellationToken ct);
    Task<MeetingDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<MeetingDto> CreateAsync(CreateMeetingRequest request, CancellationToken ct);
    Task<MeetingDto?> UpdateAsync(Guid id, CreateMeetingRequest request, CancellationToken ct);
    Task<MeetingDto?> SetAttendanceAsync(Guid id, bool? isAttended, CancellationToken ct);
    Task<MeetingDto?> SetAttendeeAttendanceAsync(Guid meetingId, Guid dokCaseId, bool? isAttended, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
}
