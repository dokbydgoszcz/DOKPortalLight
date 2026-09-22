namespace DokPortal.Application.ParishNeeds;

public class CreateParishNeedRequest
{
    public required Guid ParishId { get; init; }
    public required string Description { get; init; }
}
