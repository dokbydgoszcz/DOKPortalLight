namespace DokPortal.Application.People;

/// <summary>Karta osoby: wszystko, co ją dotyczy, w jednym miejscu. Sekcja jest null, gdy oglądający nie ma do niej dostępu.</summary>
public class PersonOverviewDto
{
    public required PersonDto Person { get; init; }
    public IReadOnlyList<OverviewCaseDto>? DokCases { get; init; }
    /// <summary>Sprawy, które osoba prowadzi jako katechista lub w których jest mentorem.</summary>
    public IReadOnlyList<OverviewCaseDto>? CasesLed { get; init; }
    public OverviewCandidateDto? Candidate { get; init; }
    public IReadOnlyList<OverviewMissionDto>? Missions { get; init; }
    public IReadOnlyList<OverviewAssignmentDto>? ParishAssignments { get; init; }
    public IReadOnlyList<string>? FormatorFunctions { get; init; }
    public IReadOnlyList<OverviewDocumentDto>? Documents { get; init; }
    public OverviewSupervisionsDto? Supervisions { get; init; }
    public OverviewAccountDto? Account { get; init; }
    /// <summary>Czy pokazujemy sekcję (dostęp), niezależnie od tego, czy coś w niej jest.</summary>
    public bool CandidateVisible { get; init; }
    public bool AccountVisible { get; init; }
}

public class OverviewCaseDto
{
    public required Guid Id { get; init; }
    public required string PersonFullName { get; init; }
    public required string Path { get; init; }
    public required string Stage { get; init; }
    public required string CatechistFullName { get; init; }
    /// <summary>„podopieczny”, „katechista” albo „mentor”.</summary>
    public required string Role { get; init; }
    public DateOnly? InterruptedOn { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
}

public class OverviewCandidateDto
{
    public required Guid Id { get; init; }
    public required int FormationYear { get; init; }
    public required string Status { get; init; }
    public int? AttendancePercentage { get; init; }
    public int OpinionsCollected { get; init; }
    public int OpinionsRequired { get; init; }
}

public class OverviewMissionDto
{
    public required Guid Id { get; init; }
    public required string ServicePlace { get; init; }
    public DateOnly MissionStartDate { get; init; }
    public DateOnly MissionEndDate { get; init; }
    public string? SupervisionGroup { get; init; }
    public required string Status { get; init; }
}

public class OverviewAssignmentDto
{
    public required string ParishName { get; init; }
    public required string NeedDescription { get; init; }
    public DateTime AssignedAtUtc { get; init; }
}

public class OverviewDocumentDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public class OverviewSupervisionsDto
{
    public int Present { get; init; }
    public int Listed { get; init; }
    public DateOnly? LastPresentOn { get; init; }
}

public class OverviewAccountDto
{
    public required string Email { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>Do których sekcji karty osoby oglądający ma dostęp.</summary>
public sealed record PersonOverviewAccess(
    bool DokCases, bool Candidates, bool Missions, bool ParishNeeds, bool Formators, bool Documents, bool Supervisions, bool Users);

public interface IPersonOverviewService
{
    Task<PersonOverviewDto?> GetAsync(Guid personId, PersonOverviewAccess access, CancellationToken ct);
}
