namespace DokPortal.Application.Meetings;

public class CreateMeetingRequest
{
    public Guid? DokCaseId { get; init; }
    public string? GroupLabel { get; init; }
    public required DateOnly MeetingDate { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    public bool? IsAttended { get; init; }
    public string? Notes { get; init; }
    /// <summary>Uczestnicy zajęć grupowych (wyklucza się z DokCaseId).</summary>
    public IReadOnlyList<AttendeeRequest>? Attendees { get; init; }
    /// <summary>Tylko przy tworzeniu: zakłada serię spotkań co tydzień do tej daty włącznie.</summary>
    public DateOnly? RepeatWeeklyUntil { get; init; }
}
