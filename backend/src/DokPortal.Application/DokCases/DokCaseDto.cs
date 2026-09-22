using DokPortal.Domain.Enums;

namespace DokPortal.Application.DokCases;

public class DokCaseDto
{
    public required Guid Id { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public string? ParishName { get; init; }
    public required DokPath Path { get; init; }
    public required DokStage Stage { get; init; }
    public required Guid CatechistPersonId { get; init; }
    public required string CatechistFullName { get; init; }
    public Guid? MentorPersonId { get; init; }
    public string? MentorFullName { get; init; }
    public DateOnly? LastMeetingDate { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
}
