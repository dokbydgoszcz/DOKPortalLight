namespace DokPortal.Application.Meetings;

public class CreateMeetingRequest
{
    public Guid? DokCaseId { get; init; }
    public string? GroupLabel { get; init; }
    public required DateOnly MeetingDate { get; init; }
    public bool? IsAttended { get; init; }
    public string? Notes { get; init; }
}
