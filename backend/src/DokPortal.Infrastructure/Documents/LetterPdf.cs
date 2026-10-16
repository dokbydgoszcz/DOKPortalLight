using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DokPortal.Infrastructure.Documents;

/// <summary>Papier firmowy: tekst obok logo, stopka; logo opcjonalne.</summary>
public sealed record LetterheadContent(string? HeaderText, string? FooterText, byte[]? Logo);

/// <summary>Pismo jako PDF (A4).</summary>
public static class LetterPdf
{
    static LetterPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Render(IReadOnlyList<LetterBlock> blocks, LetterheadContent? letterhead)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(2, Unit.Centimetre);
                page.MarginVertical(1.6f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(11).LineHeight(1.35f));

                if (letterhead is not null && (letterhead.Logo is not null || !string.IsNullOrWhiteSpace(letterhead.HeaderText)))
                {
                    page.Header().PaddingBottom(14).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            if (letterhead.Logo is { } logo)
                            {
                                row.ConstantItem(62).Height(62).Image(logo).FitArea();
                                row.ConstantItem(12);
                            }
                            row.RelativeItem().AlignMiddle().Column(text =>
                            {
                                var lines = (letterhead.HeaderText ?? "").Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
                                for (var i = 0; i < lines.Length; i++)
                                {
                                    var line = text.Item().Text(lines[i].Trim());
                                    if (i == 0) line.FontSize(13).Bold(); else line.FontSize(9).FontColor(Colors.Grey.Darken2);
                                }
                            });
                        });
                        col.Item().PaddingTop(8).LineHorizontal(0.75f).LineColor(Colors.Grey.Lighten1);
                    });
                }

                page.Content().Column(col =>
                {
                    col.Spacing(4);
                    foreach (var block in blocks) RenderBlock(col.Item(), block);
                });

                page.Footer().Column(col =>
                {
                    if (letterhead is not null && !string.IsNullOrWhiteSpace(letterhead.FooterText))
                    {
                        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(4).AlignCenter().Text(letterhead.FooterText.Trim()).FontSize(8).FontColor(Colors.Grey.Darken1);
                    }
                    col.Item().AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium));
                        t.CurrentPageNumber();
                        t.Span(" / ");
                        t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void RenderBlock(IContainer item, LetterBlock block)
    {
        switch (block.Kind)
        {
            case LetterBlockKind.Rule:
                item.PaddingVertical(4).LineHorizontal(0.75f).LineColor(Colors.Grey.Medium);
                return;
            case LetterBlockKind.Bullet:
            case LetterBlockKind.Numbered:
                item.PaddingLeft(10).Row(row =>
                {
                    row.ConstantItem(18).Text(block.Kind == LetterBlockKind.Bullet ? "•" : $"{block.Number}.");
                    row.RelativeItem().Text(t => Spans(t, block));
                });
                return;
        }
        var container = block.Kind switch
        {
            LetterBlockKind.Heading1 => item.PaddingTop(8).PaddingBottom(4),
            LetterBlockKind.Heading2 => item.PaddingTop(6).PaddingBottom(3),
            LetterBlockKind.Heading3 => item.PaddingTop(4).PaddingBottom(2),
            _ => item
        };
        container.Text(t =>
        {
            switch (block.Align)
            {
                case LetterAlign.Center: t.AlignCenter(); break;
                case LetterAlign.Right: t.AlignRight(); break;
                case LetterAlign.Justify: t.Justify(); break;
            }
            Spans(t, block);
        });
    }

    private static void Spans(TextDescriptor t, LetterBlock block)
    {
        if (block.Runs.Count == 0)
        {
            t.Span(" ");
            return;
        }
        var size = block.Kind switch { LetterBlockKind.Heading1 => 18f, LetterBlockKind.Heading2 => 15f, LetterBlockKind.Heading3 => 12.5f, _ => 11f };
        var heading = block.Kind is LetterBlockKind.Heading1 or LetterBlockKind.Heading2 or LetterBlockKind.Heading3;
        foreach (var run in block.Runs)
        {
            if (run.Break)
            {
                t.Span("\n");
                continue;
            }
            var span = t.Span(run.Text).FontSize(size);
            if (run.Bold || heading) span.Bold();
            if (run.Italic) span.Italic();
            if (run.Underline) span.Underline();
        }
    }
}
