namespace DokPortal.Application.Candidates;

public class CandidateDto
{
    public required Guid Id { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public string? ParishName { get; init; }
    /// <summary>Bieżący rok formacji 1–3 (po ukończeniu nadal 3).</summary>
    public required int Year { get; init; }
    public required string Status { get; init; }
    public bool IsFormationStopped { get; init; }
    public string? FormationStopNote { get; init; }
    public int? AttendancePercentage { get; init; }
    public required int OpinionsCollected { get; init; }
    public required int OpinionsRequired { get; init; }
    public required IReadOnlyList<CandidateRetreatDto> Retreats { get; init; }
}
