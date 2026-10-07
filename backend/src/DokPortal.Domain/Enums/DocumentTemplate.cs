namespace DokPortal.Domain.Enums;

/// <summary>Typy pism. Wartości liczbowe są zapisane w historii pism – nie zmieniać istniejących.</summary>
public enum DocumentTemplate
{
    LetterToBishop = 0,
    ConversionConsent = 1,
    /// <summary>Dawniej „Dekret misji kanonicznej”; teraz wniosek o misję kanoniczną.</summary>
    CanonicalMissionApplication = 2,
    DokReferral = 3,
    SkspCompletionCertificate = 4,
    /// <summary>Dawne ogólne zaświadczenie o sakramencie – tylko w historii; zastąpione zaświadczeniami o poszczególnych sakramentach.</summary>
    SacramentCertificate = 5,
    BaptismCertificate = 6,
    ConfirmationCertificate = 7,
    EucharistCertificate = 8,
    CatechumenateStudyCertificate = 9,
    GodparentCertificate = 10,
    GdprClause = 11
}
