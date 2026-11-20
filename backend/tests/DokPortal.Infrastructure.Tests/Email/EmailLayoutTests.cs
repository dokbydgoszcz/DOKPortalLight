using System.Net.Mail;
using DokPortal.Infrastructure.Email;
using DokPortal.Infrastructure.Services;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Email;

/// <summary>Każdy e-mail z portalu dostaje wersję HTML w barwach portalu, zrobioną z treści tekstowej.</summary>
public class EmailLayoutTests
{
    private const string Portal = "https://portal.example.org";

    [Fact]
    public void APasswordResetLink_BecomesAButtonWithTheAddressBelow()
    {
        var link = $"{Portal}/reset-hasla?uid=1&token=a%2Bb";
        var html = EmailLayout.Render("Zmiana hasła w DOK Portal",
            $"Dzień dobry,\n\nktoś poprosił o zmianę hasła dla konta jan@example.org.\n\nAby ustawić nowe hasło, otwórz link:\n{link}\n\nDOK Portal", Portal);

        Assert.Contains("<h1", html);
        Assert.Contains("Zmiana hasła w DOK Portal</h1>", html);
        Assert.Contains("Ustaw nowe hasło &rarr;</a>", html);
        Assert.Contains($"href=\"{Portal}/reset-hasla?uid=1&amp;token=a%2Bb\"", html);
        Assert.Contains("Jeśli przycisk nie działa", html);
        // Krzyż w logo jest narysowany, nie znakiem – Gmail/Outlook robią z „✝” fioletowe emoji.
        Assert.DoesNotContain("&#10013;", html);
        Assert.DoesNotContain("✝", html);
        Assert.Contains("bgcolor=\"#10302D\"", html);
        Assert.Contains("DOK Portal", html);
        Assert.Contains($"href=\"{Portal}\"", html);
        // Podgląd w skrzynce pomija powitanie.
        Assert.Contains("opacity:0;color:#F6F5F1;\">ktoś poprosił o zmianę hasła dla konta jan@example.org.</div>", html);
    }

    [Fact]
    public void ListsLinksAndTheFooter_AreFormatted_AndUserTextIsEscaped()
    {
        var html = EmailLayout.Render("Imieniny <tydzień>",
            "Imieniny w najbliższym tygodniu:\n- Jan <b>Kowalski</b> — 24.06\n• Anna & Ewa\n\nWięcej: https://example.org/a, oraz tekst.\n\n—\nWiadomość z DOK Portal.\nWypisz się: https://portal.example.org/wypisz/abc");

        Assert.Contains("Imieniny &lt;tydzień&gt;", html);
        Assert.Contains("Jan &lt;b&gt;Kowalski&lt;/b&gt; — 24.06", html);
        Assert.Contains("Anna &amp; Ewa", html);
        Assert.Equal(2, html.Split("&#8226;").Length - 1);
        Assert.Contains("<a href=\"https://example.org/a\"", html);
        Assert.Contains("</a>, oraz tekst.", html);
        // Link w stopce zostaje zwykłym, drobnym linkiem, a nie przyciskiem.
        Assert.Contains("font-size:12px", html);
        Assert.Contains("href=\"https://portal.example.org/wypisz/abc\"", html);
        Assert.DoesNotContain("Wypisz się z wiadomości &rarr;", html);
        Assert.DoesNotContain("Otwórz DOK Portal", html);
    }

    [Theory]
    [InlineData("https://portal.example.org/reset-hasla?x=1", "Ustaw nowe hasło")]
    [InlineData("https://portal.example.org/reset-hasla?uid=1&token=x&zaproszenie=1", "Ustaw hasło i wejdź")]
    [InlineData("https://portal.example.org/wypisz/abc", "Wypisz się z wiadomości")]
    [InlineData("https://portal.example.org/dok-cases", "Otwórz w DOK Portal")]
    [InlineData("https://inna.example.org/x", "Otwórz link")]
    public void ButtonsAreNamedAfterWhereTheyLead(string url, string label) => Assert.Equal(label, EmailLayout.ButtonLabel(url, Portal));

    [Fact]
    public void AStandaloneLink_IsAButton_EvenWithoutKnowingThePortalAddress()
    {
        var html = EmailLayout.Render("Test", "Zobacz:\nhttps://example.org/x\n\n\n\nKoniec", null);
        Assert.Contains("Otwórz link &rarr;", html);
        Assert.Contains("Koniec", html);
        Assert.DoesNotContain("&#243;", html);
    }

    [Fact]
    public void TheSender_AddsTheHtmlBesideThePlainText()
    {
        using var view = SmtpEmailSender.BuildHtmlView("Temat", "Treść", Portal);
        Assert.Equal("text/html", view.ContentType.MediaType);
        Assert.Equal(System.Net.Mime.TransferEncoding.QuotedPrintable, view.TransferEncoding);
        using var reader = new StreamReader(view.ContentStream);
        Assert.Contains("Temat</h1>", reader.ReadToEnd());
    }
}
