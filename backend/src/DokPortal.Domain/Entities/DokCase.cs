using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class DokCase
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public DokPath Path { get; set; }
    public DokStage Stage { get; set; }
    public Guid CatechistPersonId { get; set; }
    public Person? CatechistPerson { get; set; }
    public Guid? MentorPersonId { get; set; }
    public Person? MentorPerson { get; set; }
    public DateOnly? LastMeetingDate { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
