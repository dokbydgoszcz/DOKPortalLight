namespace DokPortal.Application.Meetings;

public class MeetingAttendeeDto
{
    public required Guid DokCaseId { get; init; }
    public required string PersonFullName { get; init; }
    public bool? IsAttended { get; init; }
}
