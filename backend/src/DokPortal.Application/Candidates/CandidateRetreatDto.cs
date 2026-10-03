namespace DokPortal.Application.Candidates;

/// <summary>Rekolekcje w jednym roku formacji; ten sam kształt służy do odczytu i zapisu.</summary>
public class CandidateRetreatDto
{
    public required int Year { get; init; }
    public required bool IsCompleted { get; init; }
}
