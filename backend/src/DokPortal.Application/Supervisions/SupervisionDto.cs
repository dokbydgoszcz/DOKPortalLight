using DokPortal.Domain.Enums;

namespace DokPortal.Application.Supervisions;

public class SupervisionDto
{
    public required Guid Id { get; init; }
    public required Institution Institution { get; init; }
    public required string GroupLabel { get; init; }
    public required DateOnly SupervisionDate { get; init; }
    public int? AttendeesCount { get; init; }
    public int? ExpectedCount { get; init; }
    public string? Topic { get; init; }
    public string? Conclusion { get; init; }
}
