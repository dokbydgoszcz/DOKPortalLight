namespace DokPortal.Application.PastoralNotes;

/// <summary>Stan udostępnienia notatek bieżącego użytkownika w sprawie.</summary>
public class NoteSharingDto
{
    /// <summary>Podopieczny sprawy (do opisu, także gdy autor nie ma już dostępu do sprawy).</summary>
    public required string CasePersonFullName { get; init; }
    /// <summary>Czy użytkownik ma w tej sprawie własne notatki (tylko je może udostępnić).</summary>
    public required bool HasOwnNotes { get; init; }
    public required Guid CurrentCatechistPersonId { get; init; }
    public required string CurrentCatechistFullName { get; init; }
    /// <summary>Można udostępnić: są własne notatki, katechistą jest ktoś inny i jeszcze nie udostępniono mu notatek.</summary>
    public required bool CanShareWithCurrentCatechist { get; init; }
    public required IReadOnlyList<NoteShareDto> SharedWith { get; init; }
}

/// <summary>Sprawa, w której użytkownik ma notatki, a prowadzi ją już ktoś inny.</summary>
public class HandedOverCaseDto
{
    public required Guid CaseId { get; init; }
    public required string PersonFullName { get; init; }
    public required Guid CurrentCatechistPersonId { get; init; }
    public required string CurrentCatechistFullName { get; init; }
    public required int NotesCount { get; init; }
    public required bool SharedWithCurrentCatechist { get; init; }
}

public class NoteShareDto
{
    public required Guid PersonId { get; init; }
    public required string FullName { get; init; }
    public required DateTime SharedAtUtc { get; init; }
}
