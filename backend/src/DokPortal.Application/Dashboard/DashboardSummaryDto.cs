namespace DokPortal.Application.Dashboard;

public class DashboardSummaryDto
{
    public required int PeopleCount { get; init; }
    public required int ParishCount { get; init; }
    public required IReadOnlyList<DokStageCountDto> DokCasesByStage { get; init; }
    public required int MissingDocumentsCasesCount { get; init; }
    public required int UpcomingMeetingsCount { get; init; }
    /// <summary>Spotkania z najbliższych 7 dni (od najwcześniejszego), co najwyżej 10; liczba całkowita jest w UpcomingMeetingsCount.</summary>
    public IReadOnlyList<UpcomingMeetingDto> UpcomingMeetings { get; init; } = Array.Empty<UpcomingMeetingDto>();
    /// <summary>Sprawy DOK z brakującymi dokumentami (alfabetycznie wg podopiecznego), co najwyżej 20; liczba całkowita jest w MissingDocumentsCasesCount.</summary>
    public IReadOnlyList<MissingDocumentsCaseDto> MissingDocumentsCases { get; init; } = Array.Empty<MissingDocumentsCaseDto>();
    public required int ActiveCandidatesCount { get; init; }
    /// <summary>Podopieczni (poza absolwentami) na jednym etapie dłużej niż rok – najdłużej czekający pierwsi, co najwyżej 20.</summary>
    public IReadOnlyList<StalledCaseDto> StalledCases { get; init; } = Array.Empty<StalledCaseDto>();
    /// <summary>Ile takich spraw jest w sumie (lista pokazuje tylko pierwsze 20).</summary>
    public int StalledCasesCount { get; init; }
}

public class UpcomingMeetingDto
{
    public required Guid MeetingId { get; init; }
    public required DateOnly MeetingDate { get; init; }
    /// <summary>Podopieczny (spotkanie indywidualne) albo nazwa grupy.</summary>
    public required string Label { get; init; }
}

public class MissingDocumentsCaseDto
{
    public required Guid CaseId { get; init; }
    public required string PersonFullName { get; init; }
    public required string Path { get; init; }
    public required IReadOnlyList<string> MissingDocuments { get; init; }
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
