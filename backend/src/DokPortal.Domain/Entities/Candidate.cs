namespace DokPortal.Domain.Entities;

public class Candidate : ISoftDeletable
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    /// <summary>Bieżący rok formacji 1–3. Zmienia się tylko ręcznie (przycisk „Przenieś” albo edycja).</summary>
    public int FormationYear { get; set; } = 1;
    /// <summary>Kandydat ukończył III rok formacji.</summary>
    public bool IsFormationCompleted { get; set; }
    /// <summary>Od kiedy kandydat jest w obecnym roku (przy ukończeniu: kiedy ukończył formację).</summary>
    public DateTime FormationYearSinceUtc { get; set; } = DateTime.UtcNow;
    public ICollection<CandidateFormationEvent> Events { get; set; } = new List<CandidateFormationEvent>();
    /// <summary>Formacja zatrzymana ręcznie; wymaga notatki z powodem.</summary>
    public bool IsFormationStopped { get; set; }
    public string? FormationStopNote { get; set; }
    public int? AttendancePercentage { get; set; }
    public int OpinionsCollected { get; set; }
    public int OpinionsRequired { get; set; } = 2;
    public ICollection<CandidateRetreat> Retreats { get; set; } = new List<CandidateRetreat>();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
