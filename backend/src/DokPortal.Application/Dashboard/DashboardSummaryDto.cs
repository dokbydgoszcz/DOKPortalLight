namespace DokPortal.Application.Dashboard;

public class DashboardSummaryDto
{
    public required int PeopleCount { get; init; }
    public required int ParishCount { get; init; }
    public required IReadOnlyList<DokStageCountDto> DokCasesByStage { get; init; }
    public required int MissingDocumentsCasesCount { get; init; }
    public required int UpcomingMeetingsCount { get; init; }
    public required int ActiveCandidatesCount { get; init; }
}

public class DokStageCountDto
{
    public required string Stage { get; init; }
    public required int Count { get; init; }
}
