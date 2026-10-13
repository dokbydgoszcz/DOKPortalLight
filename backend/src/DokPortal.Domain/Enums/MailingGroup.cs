namespace DokPortal.Domain.Enums;

public enum MailingGroup
{
    CandidatesSksp = 0,
    Missionaries = 1,
    DokGraduates = 2,
    DokCases = 3,
    /// <summary>Osoby z funkcją Proboszcz.</summary>
    Pastors = 4,
    /// <summary>Formatorzy SKŚP.</summary>
    Formators = 5,
    /// <summary>Własna lista – osoby wybrane w kampanii.</summary>
    Custom = 6
}
