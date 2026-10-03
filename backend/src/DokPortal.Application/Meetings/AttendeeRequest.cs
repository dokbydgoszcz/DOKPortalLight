namespace DokPortal.Application.Meetings;

public class AttendeeRequest
{
    public required Guid DokCaseId { get; init; }
    public bool? IsAttended { get; init; }
}
