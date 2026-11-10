using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace DokPortal.Infrastructure.Documents;

/// <summary>
/// Czyści treść wzoru pisma przed zapisem: zostają tylko znaczniki, które edytor tworzy, a pisma rozumieją
/// (akapity, nagłówki, listy, pogrubienie…), i tylko wyrównanie/pogrubienie w stylu. Skrypty, ramki, obrazki,
/// odnośniki, atrybuty zdarzeń (onclick…) i wszystko inne znika – treść wzoru otwiera potem w przeglądarce
/// inna osoba, więc nie może nieść niczego wykonywalnego.
/// </summary>
public static class LetterHtml
{
    public const int MaxLength = 200_000;

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "strong", "b", "em", "i", "u", "s", "h1", "h2", "h3", "h4", "h5", "h6",
        "ul", "ol", "li", "blockquote", "hr", "span", "div"
    };

    /// <summary>Znaczniki usuwane razem z zawartością (reszta nieznanych jest „rozpakowywana” – tekst zostaje).</summary>
    private static readonly HashSet<string> Dropped = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "frame", "frameset", "object", "embed", "applet", "svg", "math", "link", "meta", "base",
        "form", "input", "button", "select", "textarea", "template", "noscript", "head", "title", "audio", "video", "img", "picture", "source", "canvas"
    };

    private static readonly string[] Alignments = { "left", "right", "center", "justify" };

    public static string Sanitize(string html)
    {
        var document = new HtmlParser().ParseDocument("<!doctype html><html><body>" + html + "</body></html>");
        Clean(document.Body!);
        return document.Body!.InnerHtml;
    }

    private static void Clean(IElement parent)
    {
        foreach (var node in parent.ChildNodes.ToList())
        {
            if (node.NodeType == NodeType.Text) continue;
            if (node is not IElement element)
            {
                parent.RemoveChild(node); // komentarze, instrukcje przetwarzania
                continue;
            }
            if (Dropped.Contains(element.LocalName) || element.NamespaceUri != "http://www.w3.org/1999/xhtml")
            {
                parent.RemoveChild(element);
                continue;
            }
            Clean(element);
            if (!Allowed.Contains(element.LocalName))
            {
                while (element.FirstChild is { } child) parent.InsertBefore(child, element);
                parent.RemoveChild(element);
                continue;
            }
            var style = SafeStyle(element.GetAttribute("style"));
            foreach (var attribute in element.Attributes.Select(a => a.Name).ToList()) element.RemoveAttribute(attribute);
            if (style is not null) element.SetAttribute("style", style);
        }
    }

    private static string? SafeStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style)) return null;
        var kept = new List<string>();
        foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = declaration.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;
            var (name, value) = (parts[0].ToLowerInvariant(), parts[1].ToLowerInvariant());
            if (name == "text-align" && Alignments.Contains(value)) kept.Add($"text-align: {value}");
            else if (name == "font-weight" && value is "bold" or "700") kept.Add("font-weight: bold");
        }
        return kept.Count == 0 ? null : string.Join("; ", kept);
    }
}
