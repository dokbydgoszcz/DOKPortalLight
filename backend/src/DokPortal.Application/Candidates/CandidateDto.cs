namespace DokPortal.Application.Candidates;

public class CandidateDto
{
    public required Guid Id { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public string? ParishName { get; init; }
    public required int Year { get; init; }
    public int? AttendancePercentage { get; init; }
    public required int OpinionsCollected { get; init; }
    public required int OpinionsRequired { get; init; }
    public required bool IsRetreatCompleted { get; init; }
}
