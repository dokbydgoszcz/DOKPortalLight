namespace DokPortal.Domain.Entities;

/// <summary>Uczestnik zajęć grupowych: sprawa DOK podopiecznego i jego obecność na danym spotkaniu.</summary>
public class MeetingAttendee
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public Meeting? Meeting { get; set; }
    public Guid DokCaseId { get; set; }
    public DokCase? DokCase { get; set; }
    public bool? IsAttended { get; set; }
}
