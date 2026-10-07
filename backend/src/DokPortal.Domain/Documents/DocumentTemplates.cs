using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Documents;

/// <summary>Tytuły pism i lista typów, które można dziś wygenerować (dawne typy zostają tylko w historii).</summary>
public static class DocumentTemplates
{
    /// <summary>Typy dostępne przy generowaniu, w kolejności menu.</summary>
    public static IReadOnlyList<DocumentTemplate> Offered { get; } = new[]
    {
        DocumentTemplate.LetterToBishop,
        DocumentTemplate.ConversionConsent,
        DocumentTemplate.CanonicalMissionApplication,
        DocumentTemplate.DokReferral,
        DocumentTemplate.SkspCompletionCertificate,
        DocumentTemplate.CatechumenateStudyCertificate,
        DocumentTemplate.BaptismCertificate,
        DocumentTemplate.ConfirmationCertificate,
        DocumentTemplate.EucharistCertificate,
        DocumentTemplate.GodparentCertificate,
        DocumentTemplate.GdprClause
    };

    public static bool IsOffered(DocumentTemplate template) => Offered.Contains(template);

    public static string Title(DocumentTemplate template) => template switch
    {
        DocumentTemplate.LetterToBishop => "Pismo do Biskupa",
        DocumentTemplate.ConversionConsent => "Zgoda na konwersję",
        DocumentTemplate.CanonicalMissionApplication => "Wniosek o misję kanoniczną",
        DocumentTemplate.DokReferral => "Skierowanie do DOK",
        DocumentTemplate.SkspCompletionCertificate => "Zaświadczenie ukończenia SKŚP",
        DocumentTemplate.SacramentCertificate => "Zaświadczenie o sakramencie (dawne)",
        DocumentTemplate.BaptismCertificate => "Zaświadczenie o chrzcie",
        DocumentTemplate.ConfirmationCertificate => "Zaświadczenie o bierzmowaniu",
        DocumentTemplate.EucharistCertificate => "Zaświadczenie o Eucharystii",
        DocumentTemplate.CatechumenateStudyCertificate => "Zaświadczenie o ukończeniu studium katechumenalnego",
        DocumentTemplate.GodparentCertificate => "Zaświadczenie – ojciec chrzestny / matka chrzestna",
        DocumentTemplate.GdprClause => "Klauzula RODO",
        _ => template.ToString()
    };
}
