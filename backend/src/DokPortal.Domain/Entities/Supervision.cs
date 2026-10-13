using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class Supervision : ISoftDeletable
{
    public Guid Id { get; set; }
    public Institution Institution { get; set; }
    public required string GroupLabel { get; set; }
    public DateOnly SupervisionDate { get; set; }
    public int? AttendeesCount { get; set; }
    public int? ExpectedCount { get; set; }
    public string? Topic { get; set; }
    public string? Conclusion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
    /// <summary>Imienna lista obecności (katechiści z grupy).</summary>
    public List<SupervisionAttendee> Attendees { get; set; } = new();
}

/// <summary>Katechista na liście obecności superwizji.</summary>
public class SupervisionAttendee
{
    public Guid SupervisionId { get; set; }
    public Supervision? Supervision { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public bool IsPresent { get; set; }
}
