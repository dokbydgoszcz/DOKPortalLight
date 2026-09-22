namespace DokPortal.Domain.Entities;

public class Candidate
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public int Year { get; set; }
    public int? AttendancePercentage { get; set; }
    public int OpinionsCollected { get; set; }
    public int OpinionsRequired { get; set; } = 2;
    public bool IsRetreatCompleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
