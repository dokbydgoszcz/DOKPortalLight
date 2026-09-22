namespace DokPortal.Application.Missions;

public class MissionDto
{
    public required Guid Id { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public required string ServicePlace { get; init; }
    public required DateOnly MissionStartDate { get; init; }
    public required DateOnly MissionEndDate { get; init; }
    public DateOnly? GrantedDate { get; init; }
    public string? GrantedPlace { get; init; }
    public string? SupervisionGroup { get; init; }
    public required string Status { get; init; }
}
