namespace DokPortal.Domain.Entities;

/// <summary>Osoba skierowana do zapotrzebowania parafii; jedno zapotrzebowanie może mieć wiele takich osób.</summary>
public class ParishNeedAssignment
{
    public Guid Id { get; set; }
    public Guid ParishNeedId { get; set; }
    public ParishNeed? ParishNeed { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public DateTime AssignedAtUtc { get; set; }
}
