namespace DokPortal.Application.Dashboard;

public class DashboardSummaryDto
{
    public required int PeopleCount { get; init; }
    public required int ParishCount { get; init; }
    public required IReadOnlyList<DokStageCountDto> DokCasesByStage { get; init; }
    public required int MissingDocumentsCasesCount { get; init; }
    public required int UpcomingMeetingsCount { get; init; }
    public required int ActiveCandidatesCount { get; init; }
    /// <summary>Podopieczni (poza absolwentami) na jednym etapie dłużej niż rok – najdłużej czekający pierwsi, co najwyżej 20.</summary>
    public IReadOnlyList<StalledCaseDto> StalledCases { get; init; } = Array.Empty<StalledCaseDto>();
    /// <summary>Ile takich spraw jest w sumie (lista pokazuje tylko pierwsze 20).</summary>
    public int StalledCasesCount { get; init; }
}

public class StalledCaseDto
{
    public required Guid CaseId { get; init; }
    public required string PersonFullName { get; init; }
    public required string Path { get; init; }
    public required string Stage { get; init; }
    public required DateTime StageSinceUtc { get; init; }
    /// <summary>Pełne miesiące na tym etapie.</summary>
    public required int MonthsOnStage { get; init; }
}

public class DokStageCountDto
{
    public required string Stage { get; init; }
    public required int Count { get; init; }
}
