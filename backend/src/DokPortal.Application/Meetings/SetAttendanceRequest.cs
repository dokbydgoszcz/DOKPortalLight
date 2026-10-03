namespace DokPortal.Application.Meetings;

public class SetAttendanceRequest
{
    /// <summary>true = obecny, false = nieobecny, null = brak zapisanej obecności.</summary>
    public bool? IsAttended { get; init; }
}
