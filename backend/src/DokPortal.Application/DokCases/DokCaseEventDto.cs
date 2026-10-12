namespace DokPortal.Application.DokCases;

/// <summary>Wpis historii sprawy DOK, gotowy do pokazania (np. „Etap: Ewangelizacja → Absolwent”).</summary>
public class DokCaseEventDto
{
    public required string Kind { get; init; }
    public required string Description { get; init; }
    public required DateTime AtUtc { get; init; }
    public string? PerformedByEmail { get; init; }
}
