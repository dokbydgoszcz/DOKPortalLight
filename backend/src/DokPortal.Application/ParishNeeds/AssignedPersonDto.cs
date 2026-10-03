namespace DokPortal.Application.ParishNeeds;

public class AssignedPersonDto
{
    public required Guid PersonId { get; init; }
    public required string FullName { get; init; }
    public required DateTime AssignedAtUtc { get; init; }
}
