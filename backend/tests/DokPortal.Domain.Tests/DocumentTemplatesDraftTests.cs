using DokPortal.Domain.Documents;
using Xunit;

namespace DokPortal.Domain.Tests;

/// <summary>Do czasu zatwierdzenia wzorów każde pismo jest oznaczane „wzór do uzupełnienia” (przegląd 2026-10-08, A6).</summary>
public class DocumentTemplatesDraftTests
{
    [Fact]
    public void EveryOfferedTemplate_IsStillADraft_AndTheNoticeSaysSo()
    {
        Assert.All(DocumentTemplates.Offered, t => Assert.True(DocumentTemplates.IsDraft(t)));
        Assert.Contains("WZÓR DO UZUPEŁNIENIA", DocumentTemplates.DraftNotice);
    }
}
