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
    public bool IsFormationCompleted { get; init; }
    /// <summary>Od kiedy kandydat jest w obecnym roku (przy ukończeniu: od ukończenia formacji).</summary>
    public DateTime YearSinceUtc { get; init; }
    /// <summary>Historia zmian roku, od najnowszej.</summary>
    public IReadOnlyList<CandidateFormationEventDto> Events { get; init; } = Array.Empty<CandidateFormationEventDto>();
    public bool IsFormationStopped { get; init; }
    public string? FormationStopNote { get; init; }
    public int? AttendancePercentage { get; init; }
    public required int OpinionsCollected { get; init; }
    public required int OpinionsRequired { get; init; }
    public required IReadOnlyList<CandidateRetreatDto> Retreats { get; init; }
    /// <summary>Frekwencja liczona z list obecności zajęć SKŚP (wtedy AttendancePercentage pochodzi z zajęć, a nie z ręcznego wpisu).</summary>
    public bool AttendanceFromClasses { get; init; }
    /// <summary>Zajęcia z zaznaczoną obecnością: obecny / wszystkie.</summary>
    public int ClassesAttended { get; init; }
    public int ClassesRecorded { get; init; }
    /// <summary>Czego brakuje do ukończenia formacji (pusta lista = warunki spełnione).</summary>
    public IReadOnlyList<string> CompletionGaps { get; init; } = Array.Empty<string>();
}
