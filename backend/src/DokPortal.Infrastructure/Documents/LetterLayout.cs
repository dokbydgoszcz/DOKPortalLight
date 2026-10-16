using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace DokPortal.Infrastructure.Documents;

public enum LetterBlockKind { Paragraph, Heading1, Heading2, Heading3, Bullet, Numbered, Rule }

public enum LetterAlign { Left, Center, Right, Justify }

/// <summary>Fragment tekstu z formatowaniem; <see cref="Break"/> = złamanie wiersza.</summary>
public sealed record LetterRun(string Text, bool Bold = false, bool Italic = false, bool Underline = false, bool Break = false);

public sealed record LetterBlock(LetterBlockKind Kind, LetterAlign Align, IReadOnlyList<LetterRun> Runs, int Number = 0);

/// <summary>
/// Treść pisma z edytora (HTML) jako lista bloków do wydruku. Uwzględnia tylko to, co daje edytor:
/// akapity, nagłówki, pogrubienie, kursywę, podkreślenie, listy, linię poziomą i wyrównanie. Resztę traktuje jak zwykły tekst
/// (skrypty i style są pomijane), więc treść nie może niczego „wstrzyknąć” do pliku.
/// </summary>
public static class LetterLayout
{
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase) { "script", "style", "head", "title", "iframe", "object" };

    public static IReadOnlyList<LetterBlock> Parse(string html)
    {
        var document = new HtmlParser().ParseDocument("<!doctype html><html><body>" + html + "</body></html>");
        var blocks = new List<LetterBlock>();
        var loose = new List<LetterRun>();
        foreach (var node in document.Body!.ChildNodes) Block(node, blocks, loose);
        Flush(blocks, loose);
        return blocks;
    }

    private static void Flush(List<LetterBlock> blocks, List<LetterRun> loose)
    {
        if (loose.Count == 0) return;
        if (loose.Any(r => r.Break || r.Text.Trim().Length > 0)) blocks.Add(new LetterBlock(LetterBlockKind.Paragraph, LetterAlign.Left, loose.ToList()));
        loose.Clear();
    }

    private static void Block(INode node, List<LetterBlock> blocks, List<LetterRun> loose)
    {
        if (node is not IElement element)
        {
            if (node.NodeType == NodeType.Text) Inline(node, loose, false, false, false);
            return;
        }
        var tag = element.LocalName;
        if (Ignored.Contains(tag)) return;
        switch (tag)
        {
            case "p":
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
            {
                Flush(blocks, loose);
                var runs = new List<LetterRun>();
                foreach (var child in element.ChildNodes) Inline(child, runs, false, false, false);
                var kind = tag switch { "h1" => LetterBlockKind.Heading1, "h2" => LetterBlockKind.Heading2, "p" => LetterBlockKind.Paragraph, _ => LetterBlockKind.Heading3 };
                blocks.Add(new LetterBlock(kind, Align(element), Trim(runs)));
                break;
            }
            case "ul":
            case "ol":
            {
                Flush(blocks, loose);
                var number = 0;
                foreach (var item in element.Children.Where(c => c.LocalName == "li"))
                {
                    var runs = new List<LetterRun>();
                    var first = true;
                    foreach (var child in item.ChildNodes)
                    {
                        if (child is IElement { LocalName: "p" } && !first) runs.Add(new LetterRun("", Break: true));
                        Inline(child, runs, false, false, false);
                        if (child is IElement { LocalName: "p" }) first = false;
                    }
                    blocks.Add(new LetterBlock(tag == "ol" ? LetterBlockKind.Numbered : LetterBlockKind.Bullet, LetterAlign.Left, Trim(runs), ++number));
                }
                break;
            }
            case "hr":
                Flush(blocks, loose);
                blocks.Add(new LetterBlock(LetterBlockKind.Rule, LetterAlign.Left, Array.Empty<LetterRun>()));
                break;
            case "div":
            case "section":
            case "article":
            case "blockquote":
                Flush(blocks, loose);
                foreach (var child in element.ChildNodes) Block(child, blocks, loose);
                Flush(blocks, loose);
                break;
            default:
                Inline(element, loose, false, false, false);
                break;
        }
    }

    private static void Inline(INode node, List<LetterRun> runs, bool bold, bool italic, bool underline)
    {
        if (node.NodeType == NodeType.Text)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(node.TextContent, @"[\r\n\t ]+", " ");
            if (text.Length > 0) runs.Add(new LetterRun(text, bold, italic, underline));
            return;
        }
        if (node is not IElement element || Ignored.Contains(element.LocalName)) return;
        switch (element.LocalName)
        {
            case "br":
                runs.Add(new LetterRun("", Break: true));
                return;
            case "strong":
            case "b":
                bold = true;
                break;
            case "em":
            case "i":
                italic = true;
                break;
            case "u":
                underline = true;
                break;
        }
        var style = element.GetAttribute("style") ?? "";
        if (style.Contains("font-weight: bold", StringComparison.OrdinalIgnoreCase)) bold = true;
        foreach (var child in element.ChildNodes) Inline(child, runs, bold, italic, underline);
    }

    private static List<LetterRun> Trim(List<LetterRun> runs)
    {
        if (runs.Count > 0 && !runs[0].Break) runs[0] = runs[0] with { Text = runs[0].Text.TrimStart() };
        if (runs.Count > 0 && !runs[^1].Break) runs[^1] = runs[^1] with { Text = runs[^1].Text.TrimEnd() };
        return runs;
    }

    private static LetterAlign Align(IElement element)
    {
        var style = (element.GetAttribute("style") ?? "").Replace(" ", "").ToLowerInvariant();
        if (style.Contains("text-align:right")) return LetterAlign.Right;
        if (style.Contains("text-align:center")) return LetterAlign.Center;
        if (style.Contains("text-align:justify")) return LetterAlign.Justify;
        return LetterAlign.Left;
    }
}
