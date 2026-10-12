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
    /// <summary>Misja, której dotyczy skierowanie (utworzona albo uzupełniona przy skierowaniu).</summary>
    public Guid? MissionId { get; set; }
    /// <summary>Misję utworzyło samo skierowanie (odpięcie ją usuwa); inaczej skierowanie tylko wpisało parafię.</summary>
    public bool MissionCreatedByAssignment { get; set; }
    /// <summary>Skierowanie wpisało parafię do wcześniejszej misji bez miejsca (odpięcie czyści miejsce).</summary>
    public bool MissionPlaceFilledByAssignment { get; set; }
}
