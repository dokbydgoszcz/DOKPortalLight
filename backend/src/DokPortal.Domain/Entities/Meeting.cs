namespace DokPortal.Domain.Entities;

public class Meeting : ISoftDeletable
{
    public Guid Id { get; set; }
    public Guid? DokCaseId { get; set; }
    public DokCase? DokCase { get; set; }
    public string? GroupLabel { get; set; }
    /// <summary>Właściciel zajęć grupowych (katechista prowadzący). Dla spotkań indywidualnych puste – wynika ze sprawy.</summary>
    public Guid? CatechistPersonId { get; set; }
    public List<MeetingAttendee> Attendees { get; set; } = new();
    public DateOnly MeetingDate { get; set; }
    public bool? IsAttended { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    public DateTime? ReminderSentAtUtc { get; set; }
}
