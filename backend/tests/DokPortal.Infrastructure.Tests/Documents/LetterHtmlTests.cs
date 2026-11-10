using DokPortal.Infrastructure.Documents;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Documents;

/// <summary>Treść wzoru pisma po zapisie nie niesie niczego wykonywalnego.</summary>
public class LetterHtmlTests
{
    [Theory]
    [InlineData("<p>Zwykły <strong>tekst</strong> i {{osoba.imię|dopełniacz}}</p>", "<p>Zwykły <strong>tekst</strong> i {{osoba.imię|dopełniacz}}</p>")]
    [InlineData("<p style=\"text-align: right; color: red; background: url(javascript:x)\">a</p>", "<p style=\"text-align: right\">a</p>")]
    [InlineData("<span style=\"font-weight:700\" class=\"x\" data-y=\"1\">a</span>", "<span style=\"font-weight: bold\">a</span>")]
    [InlineData("<p onclick=\"alert(1)\" id=\"x\">a</p>", "<p>a</p>")]
    [InlineData("<p>a<script>alert(1)</script>b</p>", "<p>ab</p>")]
    [InlineData("<img src=x onerror=alert(1)><p>a</p>", "<p>a</p>")]
    [InlineData("<a href=\"javascript:alert(1)\">klik</a>", "klik")]
    [InlineData("<iframe src=\"https://zly.example\"></iframe><p>a</p>", "<p>a</p>")]
    [InlineData("<svg><script>alert(1)</script></svg><math><mi>x</mi></math><p>a</p>", "<p>a</p>")]
    [InlineData("<p>a<!-- komentarz --></p>", "<p>a</p>")]
    [InlineData("<table><tr><td>komórka</td></tr></table>", "komórka")]
    [InlineData("<ul><li><p>jeden</p></li></ul><hr><h2 style=\"text-align:center\">T</h2>", "<ul><li><p>jeden</p></li></ul><hr><h2 style=\"text-align: center\">T</h2>")]
    [InlineData("<p style=\"text-align: lewo; bez-dwukropka\">a</p>", "<p>a</p>")]
    [InlineData("<form action=\"https://zly.example\"><input name=\"haslo\"></form><p>a &amp; b &lt; c</p>", "<p>a &amp; b &lt; c</p>")]
    public void OnlyWhatLettersUnderstandSurvives(string html, string expected) => Assert.Equal(expected, LetterHtml.Sanitize(html));
}
