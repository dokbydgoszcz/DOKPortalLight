namespace DokPortal.Application.Candidates;

public class CreateCandidateRequest
{
    public required Guid PersonId { get; init; }
    public required int Year { get; init; }
    public int? AttendancePercentage { get; init; }
    public required int OpinionsCollected { get; init; }
    public required bool IsRetreatCompleted { get; init; }
}
