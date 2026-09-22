namespace DokPortal.Domain.Entities;

public class CanonicalMission
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public required string ServicePlace { get; set; }
    public DateOnly MissionStartDate { get; set; }
    public DateOnly MissionEndDate { get; set; }
    public DateOnly? GrantedDate { get; set; }
    public string? GrantedPlace { get; set; }
    public string? SupervisionGroup { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
