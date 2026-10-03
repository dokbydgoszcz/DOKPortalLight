namespace DokPortal.Domain.Enums;

public enum CandidateFormationStatus
{
    /// <summary>Trwa formacja (rok I–III).</summary>
    InFormation,
    /// <summary>Ukończono III rok; osoba czeka na udzielenie posługi.</summary>
    Completed,
    /// <summary>Formacja zatrzymana ręcznie (ktoś się rozmyślił, nie ukończył formacji).</summary>
    Stopped
}
