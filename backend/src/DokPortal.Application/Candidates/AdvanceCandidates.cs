namespace DokPortal.Application.Candidates;

public class AdvanceCandidatesRequest
{
    public required IReadOnlyList<Guid> CandidateIds { get; init; }
}

public class SkippedCandidateDto
{
    public required Guid CandidateId { get; init; }
    public string? PersonFullName { get; init; }
    public required string Reason { get; init; }
}

public class AdvanceCandidatesResultDto
{
    /// <summary>Przeniesieni do następnego roku.</summary>
    public required int Advanced { get; init; }
    /// <summary>Ukończyli formację (byli w III roku).</summary>
    public required int Completed { get; init; }
    public required IReadOnlyList<SkippedCandidateDto> Skipped { get; init; }
}

public class CandidateFormationEventDto
{
    public required string Kind { get; init; }
    public int? FromYear { get; init; }
    public int? ToYear { get; init; }
    public required DateTime AtUtc { get; init; }
    public string? PerformedBy { get; init; }
}
