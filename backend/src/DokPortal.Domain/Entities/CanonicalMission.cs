namespace DokPortal.Domain.Entities;

public class CanonicalMission : ISoftDeletable
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public required string ServicePlace { get; set; }
    public DateOnly MissionStartDate { get; set; }
    public DateOnly MissionEndDate { get; set; }
    public DateOnly? GrantedDate { get; set; }
    public string? SupervisionGroup { get; set; }
    /// <summary>Katechista posłany do pracy w DOK.</summary>
    public bool SentToDok { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
