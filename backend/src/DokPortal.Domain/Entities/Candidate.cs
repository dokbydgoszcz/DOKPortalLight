namespace DokPortal.Domain.Entities;

public class Candidate : ISoftDeletable
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public int Year { get; set; }
    public int? AttendancePercentage { get; set; }
    public int OpinionsCollected { get; set; }
    public int OpinionsRequired { get; set; } = 2;
    public ICollection<CandidateRetreat> Retreats { get; set; } = new List<CandidateRetreat>();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
