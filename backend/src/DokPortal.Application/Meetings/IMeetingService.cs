namespace DokPortal.Application.Meetings;

public interface IMeetingService
{
    Task<IReadOnlyList<MeetingDto>> GetAllAsync(CancellationToken ct);
    Task<MeetingDto> CreateAsync(CreateMeetingRequest request, CancellationToken ct);
}
