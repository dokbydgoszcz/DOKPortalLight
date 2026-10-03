namespace DokPortal.Application.Candidates;

public class CreateCandidateRequest
{
    public required Guid PersonId { get; init; }
    public required int Year { get; init; }
    public int? AttendancePercentage { get; init; }
    public required int OpinionsCollected { get; init; }
    /// <summary>Pełna lista rekolekcji kandydata (po jednej na rok 1–3); przy edycji zastępuje dotychczasową.</summary>
    public IReadOnlyList<CandidateRetreatDto> Retreats { get; init; } = Array.Empty<CandidateRetreatDto>();
}
