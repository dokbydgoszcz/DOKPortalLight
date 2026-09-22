using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class ParishNeed
{
    public Guid Id { get; set; }
    public Guid ParishId { get; set; }
    public Parish? Parish { get; set; }
    public required string Description { get; set; }
    public ParishNeedStatus Status { get; set; } = ParishNeedStatus.Open;
    public Guid? AssignedPersonId { get; set; }
    public Person? AssignedPerson { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
