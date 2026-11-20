using System.Text;
using System.Text.RegularExpressions;

namespace DokPortal.Infrastructure.Email;

/// <summary>
/// Wersja HTML wiadomości w barwach portalu, zrobiona z jej zwykłej treści tekstowej (ta zostaje jako druga część
/// wiadomości dla programów bez HTML). Reguły odczytu tekstu:
/// <list type="bullet">
/// <item>pusta linia rozdziela akapity;</item>
/// <item>linie zaczynające się od „- ” albo „• ” to lista;</item>
/// <item>linia z samym adresem to przycisk (pod nim adres do skopiowania);</item>
/// <item>linia „—” oddziela stopkę (drobny, szary tekst).</item>
/// </list>
/// Układ oparty na tabelach i stylach w atrybutach – tak, jak wymagają tego programy pocztowe (Gmail, Outlook).
/// </summary>
public static partial class EmailLayout
{
    private const string Green = "#183D3B";
    private const string GreenDark = "#10302D";
    private const string Gold = "#E8C060";
    private const string Paper = "#F6F5F1";
    private const string Text = "#1D2522";
    private const string Muted = "#6C7A75";
    private const string Line = "#E3E8E5";
    private const string Font = "'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    public static string Render(string subject, string body, string? portalBaseUrl = null)
    {
        var normalized = body.Replace("\r\n", "\n").Trim();
        var parts = FooterSeparator().Split(normalized, 2);
        var main = parts[0].Trim();
        var footer = parts.Length > 1 ? parts[1].Trim() : null;
        var preheader = Preheader(main);

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"pl\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<meta name=\"color-scheme\" content=\"light\"><meta name=\"supported-color-schemes\" content=\"light\">")
            .Append("<title>").Append(Encode(subject)).Append("</title></head>")
            .Append($"<body style=\"margin:0;padding:0;background:{Paper};\">")
            .Append($"<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;color:{Paper};\">").Append(Encode(preheader)).Append("</div>")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:{Paper};\"><tr><td align=\"center\" style=\"padding:28px 12px;\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:600px;\">");

        // Nagłówek: zielony pasek z krzyżem i nazwą, cienka złota linia pod spodem.
        html.Append($"<tr><td style=\"background:{Green};background-image:linear-gradient(135deg,{Green} 0%,{GreenDark} 100%);border-radius:16px 16px 0 0;padding:22px 28px;\">")
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\"><tr>")
            .Append($"<td width=\"40\" height=\"40\" align=\"center\" valign=\"middle\" bgcolor=\"{Gold}\" style=\"width:40px;height:40px;border-radius:11px;background:{Gold};\">")
            .Append(Cross())
            .Append("</td>")
            .Append($"<td style=\"padding-left:14px;font-family:{Font};\">")
            .Append("<div style=\"color:#F6F5F1;font-size:17px;font-weight:700;line-height:1.2;\">DOK Portal</div>")
            .Append("<div style=\"color:rgba(246,245,241,.7);color:#B9C7C2;font-size:11px;font-weight:600;letter-spacing:.12em;text-transform:uppercase;line-height:1.6;\">Diecezjalny Ośrodek Katechumenalny</div>")
            .Append("</td></tr></table></td></tr>")
            .Append($"<tr><td style=\"height:3px;line-height:3px;font-size:0;background:{Gold};background-image:linear-gradient(90deg,{Gold},#FBE9B8,{Gold});\">&nbsp;</td></tr>");

        // Treść.
        html.Append($"<tr><td style=\"background:#FFFFFF;padding:30px 28px 26px;font-family:{Font};color:{Text};font-size:15px;line-height:1.6;\">")
            .Append($"<h1 style=\"margin:0 0 18px;font-size:21px;line-height:1.3;font-weight:700;color:{Green};\">").Append(Encode(subject)).Append("</h1>");
        foreach (var block in Blocks(main)) html.Append(RenderBlock(block, portalBaseUrl, small: false));
        html.Append("</td></tr>");

        // Stopka wiadomości (np. „wypisz się” w kampaniach) – drobnym drukiem, nadal w karcie.
        if (!string.IsNullOrEmpty(footer))
        {
            html.Append($"<tr><td style=\"background:#FFFFFF;padding:0 28px 24px;font-family:{Font};\">")
                .Append($"<div style=\"border-top:1px solid {Line};padding-top:14px;color:{Muted};font-size:12px;line-height:1.6;\">");
            foreach (var block in Blocks(footer)) html.Append(RenderBlock(block, portalBaseUrl, small: true));
            html.Append("</div></td></tr>");
        }

        html.Append($"<tr><td style=\"background:#FFFFFF;border-radius:0 0 16px 16px;height:6px;line-height:6px;font-size:0;\">&nbsp;</td></tr>")
            .Append($"<tr><td align=\"center\" style=\"padding:18px 12px 0;font-family:{Font};color:{Muted};font-size:12px;line-height:1.6;\">")
            .Append("Diecezjalny Ośrodek Katechumenalny · Szkoła Katechistów św. Pawła<br>Diecezja Bydgoska");
        if (!string.IsNullOrWhiteSpace(portalBaseUrl))
        {
            html.Append($"<br><a href=\"{Encode(portalBaseUrl)}\" style=\"color:{Green};text-decoration:underline;\">Otwórz DOK Portal</a>");
        }
        html.Append("</td></tr></table></td></tr></table></body></html>");
        return html.ToString();
    }

    /// <summary>
    /// Krzyż narysowany komórkami tabeli, nie znakiem „✝”: programy pocztowe (Gmail, Outlook) zamieniają ten znak
    /// na kolorowe emoji. Ramiona 4 px, belka poprzeczna na 1/3 wysokości.
    /// </summary>
    private static string Cross()
    {
        static string Cell(int w, int h, bool ink) =>
            $"<td width=\"{w}\" height=\"{h}\" {(ink ? $"bgcolor=\"{GreenDark}\" " : "")}style=\"width:{w}px;height:{h}px;line-height:{h}px;font-size:0;{(ink ? $"background:{GreenDark};" : "")}\">&nbsp;</td>";
        return "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" align=\"center\" style=\"margin:0 auto;border-collapse:collapse;\">" +
               "<tr>" + Cell(6, 6, false) + Cell(4, 6, true) + Cell(6, 6, false) + "</tr>" +
               "<tr>" + Cell(6, 4, true) + Cell(4, 4, true) + Cell(6, 4, true) + "</tr>" +
               "<tr>" + Cell(6, 12, false) + Cell(4, 12, true) + Cell(6, 12, false) + "</tr>" +
               "</table>";
    }

    private static IEnumerable<string[]> Blocks(string text) =>
        BlankLines().Split(text).Select(b => b.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToArray()).Where(b => b.Length > 0);

    private static string RenderBlock(string[] lines, string? portalBaseUrl, bool small)
    {
        var html = new StringBuilder();
        var paragraph = new List<string>();
        var list = new List<string>();
        var margin = small ? "0 0 8px" : "0 0 14px";

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            html.Append($"<p style=\"margin:{margin};\">").Append(string.Join("<br>", paragraph)).Append("</p>");
            paragraph.Clear();
        }
        void FlushList()
        {
            if (list.Count == 0) return;
            html.Append($"<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:{margin};\">");
            foreach (var item in list)
            {
                html.Append($"<tr><td valign=\"top\" style=\"padding:0 10px 4px 2px;color:{Gold};font-weight:700;\">&#8226;</td><td style=\"padding:0 0 4px;\">")
                    .Append(item).Append("</td></tr>");
            }
            html.Append("</table>");
            list.Clear();
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!small && StandaloneUrl().IsMatch(trimmed))
            {
                FlushParagraph();
                FlushList();
                html.Append(Button(trimmed, portalBaseUrl));
                continue;
            }
            var bullet = Bullet().Match(line);
            if (bullet.Success)
            {
                FlushParagraph();
                list.Add(Inline(bullet.Groups[1].Value));
                continue;
            }
            FlushList();
            paragraph.Add(Inline(trimmed));
        }
        FlushParagraph();
        FlushList();
        return html.ToString();
    }

    private static string Button(string url, string? portalBaseUrl)
    {
        var label = ButtonLabel(url, portalBaseUrl);
        var href = Encode(url);
        return "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:6px 0 10px;\"><tr>" +
               $"<td style=\"border-radius:10px;background:{Green};\"><a href=\"{href}\" style=\"display:inline-block;padding:13px 26px;border-radius:10px;" +
               $"background:{Green};color:#FFFFFF;font-family:{Font};font-size:15px;font-weight:700;text-decoration:none;\">{Encode(label)} &rarr;</a></td>" +
               "</tr></table>" +
               $"<p style=\"margin:0 0 16px;color:{Muted};font-size:12px;line-height:1.5;word-break:break-all;\">Jeśli przycisk nie działa, skopiuj ten adres do przeglądarki:<br>" +
               $"<a href=\"{href}\" style=\"color:{Muted};\">{Encode(url)}</a></p>";
    }

    /// <summary>Napis na przycisku z adresu: znane strony portalu mają własne nazwy.</summary>
    public static string ButtonLabel(string url, string? portalBaseUrl)
    {
        if (url.Contains("/reset-hasla", StringComparison.OrdinalIgnoreCase))
            return url.Contains("zaproszenie=1", StringComparison.OrdinalIgnoreCase) ? "Ustaw hasło i wejdź" : "Ustaw nowe hasło";
        if (url.Contains("/wypisz/", StringComparison.OrdinalIgnoreCase)) return "Wypisz się z wiadomości";
        if (!string.IsNullOrWhiteSpace(portalBaseUrl) && url.StartsWith(portalBaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) return "Otwórz w DOK Portal";
        return "Otwórz link";
    }

    /// <summary>Tekst z bezpiecznymi znakami HTML i klikalnymi adresami.</summary>
    private static string Inline(string text)
    {
        var html = new StringBuilder();
        var last = 0;
        foreach (Match match in InlineUrl().Matches(text))
        {
            html.Append(Encode(text[last..match.Index]));
            var url = match.Value.TrimEnd('.', ',', ')', ';', ':');
            html.Append($"<a href=\"{Encode(url)}\" style=\"color:{Green};text-decoration:underline;word-break:break-all;\">{Encode(url)}</a>");
            html.Append(Encode(match.Value[url.Length..]));
            last = match.Index + match.Length;
        }
        html.Append(Encode(text[last..]));
        return html.ToString();
    }

    /// <summary>Podgląd w skrzynce: pierwsze zdanie treści bez powitania.</summary>
    private static string Preheader(string main)
    {
        var lines = main.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !StandaloneUrl().IsMatch(l)).ToList();
        if (lines.Count > 1 && lines[0].EndsWith(',')) lines.RemoveAt(0);
        var first = lines.FirstOrDefault() ?? "";
        return first.Length > 140 ? first[..140] + "…" : first;
    }

    /// <summary>Tylko znaki specjalne HTML – polskie litery zostają czytelne (WebUtility.HtmlEncode zamieniłby „ó” na kod).</summary>
    private static string Encode(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    [GeneratedRegex(@"\n[ \t]*[—–-]{1,3}[ \t]*\n")] private static partial Regex FooterSeparator();
    [GeneratedRegex(@"\n[ \t]*\n")] private static partial Regex BlankLines();
    [GeneratedRegex(@"^https?://\S+$")] private static partial Regex StandaloneUrl();
    [GeneratedRegex(@"https?://[^\s<>""]+")] private static partial Regex InlineUrl();
    [GeneratedRegex(@"^\s*(?:-|•)\s+(.*)$")] private static partial Regex Bullet();
}
