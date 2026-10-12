namespace DokPortal.Domain.Enums;

/// <summary>Rodzaj zmiany w sprawie DOK. Wartości liczbowe są zapisane w bazie – nie zmieniać istniejących.</summary>
public enum DokCaseEventKind
{
    StageChanged = 1,
    CatechistChanged = 2,
    MentorChanged = 3,
    PathChanged = 4
}
