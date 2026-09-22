namespace DokPortal.Application.Meetings;

public class MeetingDto
{
    public required Guid Id { get; init; }
    public Guid? DokCaseId { get; init; }
    public string? CaseLabel { get; init; }
    public string? GroupLabel { get; init; }
    public required DateOnly MeetingDate { get; init; }
    public bool? IsAttended { get; init; }
    public string? Notes { get; init; }
}
