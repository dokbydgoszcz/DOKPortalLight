namespace DokPortal.Domain.Entities;

public class Meeting
{
    public Guid Id { get; set; }
    public Guid? DokCaseId { get; set; }
    public DokCase? DokCase { get; set; }
    public string? GroupLabel { get; set; }
    public DateOnly MeetingDate { get; set; }
    public bool? IsAttended { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
