namespace DokPortal.Application.ParishNeeds;

public class ParishNeedDto
{
    public required Guid Id { get; init; }
    public required Guid ParishId { get; init; }
    public required string ParishName { get; init; }
    public required string Description { get; init; }
    public required string Status { get; init; }
    public Guid? AssignedPersonId { get; init; }
    public string? AssignedPersonName { get; init; }
    public DateTime? AssignedAtUtc { get; init; }
}
