using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class Supervision
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
}
