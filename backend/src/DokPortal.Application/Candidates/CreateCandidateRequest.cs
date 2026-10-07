namespace DokPortal.Application.Candidates;

public class CreateCandidateRequest
{
    public required Guid PersonId { get; init; }
    /// <summary>Rok formacji 1–3, w którym kandydat jest teraz; od 1 września przestawia się sam.</summary>
    public required int Year { get; init; }
    /// <summary>Kandydat ukończył formację (III rok); wtedy rok to 3.</summary>
    public bool IsFormationCompleted { get; init; }
    public bool IsFormationStopped { get; init; }
    public string? FormationStopNote { get; init; }
    public int? AttendancePercentage { get; init; }
    public required int OpinionsCollected { get; init; }
    /// <summary>Pełna lista rekolekcji kandydata (po jednej na rok 1–3); przy edycji zastępuje dotychczasową.</summary>
    public IReadOnlyList<CandidateRetreatDto> Retreats { get; init; } = Array.Empty<CandidateRetreatDto>();
}
