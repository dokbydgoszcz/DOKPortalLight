namespace DokPortal.Domain.Enums;

public enum CandidateFormationEventKind
{
    /// <summary>Wpisanie kandydata do formacji (od wskazanego roku).</summary>
    Enrolled,
    /// <summary>Przeniesienie do następnego roku przyciskiem „Przenieś”.</summary>
    Advanced,
    /// <summary>Ukończenie formacji (po III roku).</summary>
    Completed,
    /// <summary>Zmiana roku lub cofnięcie ukończenia w edycji kandydata.</summary>
    Changed
}
