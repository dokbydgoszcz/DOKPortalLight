namespace DokPortal.Application.ParishNeeds;

public class UpdateParishNeedRequest
{
    public required Guid ParishId { get; init; }
    public required string Description { get; init; }
}
