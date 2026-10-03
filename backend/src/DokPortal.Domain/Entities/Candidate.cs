namespace DokPortal.Domain.Entities;

public class Candidate : ISoftDeletable
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    /// <summary>Rok kalendarzowy września, w którym kandydat zaczął I rok; bieżący rok formacji liczy FormationCalendar.</summary>
    public int FormationStartYear { get; set; }
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
