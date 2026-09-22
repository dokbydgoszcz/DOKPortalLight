namespace DokPortal.Application.Missions;

public class CreateMissionRequest
{
    public required Guid PersonId { get; init; }
    public required string ServicePlace { get; init; }
    public required DateOnly MissionStartDate { get; init; }
    public required DateOnly MissionEndDate { get; init; }
    public DateOnly? GrantedDate { get; init; }
    public string? GrantedPlace { get; init; }
    public string? SupervisionGroup { get; init; }
}
