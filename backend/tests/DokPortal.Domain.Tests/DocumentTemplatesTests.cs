using DokPortal.Domain.Documents;
using DokPortal.Domain.Enums;
using Xunit;

namespace DokPortal.Domain.Tests;

public class DocumentTemplatesTests
{
    [Fact]
    public void TheOfferedTemplates_AreExactlyTheCurrentOnes_InMenuOrder()
    {
        Assert.Equal(
            new[]
            {
                "Pismo do Biskupa", "Zgoda na konwersję", "Wniosek o misję kanoniczną", "Skierowanie do DOK",
                "Zaświadczenie ukończenia SKŚP", "Zaświadczenie o ukończeniu studium katechumenalnego",
                "Zaświadczenie o chrzcie", "Zaświadczenie o bierzmowaniu", "Zaświadczenie o Eucharystii",
                "Zaświadczenie – ojciec chrzestny / matka chrzestna", "Klauzula RODO"
            },
            DocumentTemplates.Offered.Select(DocumentTemplates.Title));
    }

    [Fact]
    public void ACanonicalMissionApplication_ReplacesTheDecree_UnderTheSameStoredNumber()
    {
        Assert.Equal(2, (int)DocumentTemplate.CanonicalMissionApplication);
        Assert.Equal("Wniosek o misję kanoniczną", DocumentTemplates.Title(DocumentTemplate.CanonicalMissionApplication));
    }

    [Fact]
    public void EachSacramentHasItsOwnCertificate_AndTheOldGenericOneIsOnlyHistory()
    {
        Assert.Contains(DocumentTemplate.BaptismCertificate, DocumentTemplates.Offered);
        Assert.Contains(DocumentTemplate.ConfirmationCertificate, DocumentTemplates.Offered);
        Assert.Contains(DocumentTemplate.EucharistCertificate, DocumentTemplates.Offered);
        Assert.DoesNotContain(DocumentTemplate.SacramentCertificate, DocumentTemplates.Offered);
        Assert.False(DocumentTemplates.IsOffered(DocumentTemplate.SacramentCertificate));
        Assert.Equal("Zaświadczenie o sakramencie (dawne)", DocumentTemplates.Title(DocumentTemplate.SacramentCertificate));
    }

    [Fact]
    public void EveryTemplateHasATitle_AndTheStoredNumbersNeverChange()
    {
        Assert.All(Enum.GetValues<DocumentTemplate>(), t => Assert.NotEqual(t.ToString(), DocumentTemplates.Title(t)));
        Assert.Equal(5, (int)DocumentTemplate.SacramentCertificate);
        Assert.Equal(6, (int)DocumentTemplate.BaptismCertificate);
        Assert.Equal(11, (int)DocumentTemplate.GdprClause);
    }
}
