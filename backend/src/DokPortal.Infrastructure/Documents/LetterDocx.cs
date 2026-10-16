using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace DokPortal.Infrastructure.Documents;

/// <summary>Pismo jako dokument Word (.docx, A4) – do ręcznych poprawek.</summary>
public static class LetterDocx
{
    private const string Font = "Calibri";

    public static byte[] Render(IReadOnlyList<LetterBlock> blocks, LetterheadContent? letterhead)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            var body = main.Document.Body!;
            AddStyles(main);

            foreach (var block in blocks) body.Append(Paragraph(block));

            var section = new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134, Header = 567, Footer = 567, Gutter = 0 });

            if (letterhead is not null && (letterhead.Logo is not null || !string.IsNullOrWhiteSpace(letterhead.HeaderText)))
            {
                var headerPart = main.AddNewPart<HeaderPart>();
                headerPart.Header = Header(headerPart, letterhead);
                section.PrependChild(new HeaderReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(headerPart) });
            }
            if (letterhead is not null && !string.IsNullOrWhiteSpace(letterhead.FooterText))
            {
                var footerPart = main.AddNewPart<FooterPart>();
                footerPart.Footer = new Footer(new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Center },
                        new ParagraphBorders(new TopBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF", Space = 4 })),
                    Run(letterhead.FooterText.Trim(), size: 16, color: "595959")));
                section.PrependChild(new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footerPart) });
            }
            body.Append(section);
            main.Document.Save();
        }
        return stream.ToArray();
    }

    private static void AddStyles(MainDocumentPart main)
    {
        var styles = main.AddNewPart<StyleDefinitionsPart>();
        styles.Styles = new Styles(new DocDefaults(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font, EastAsia = Font },
                new FontSize { Val = "22" }, new Languages { Val = "pl-PL" })),
            new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                new SpacingBetweenLines { After = "80", Line = "300", LineRule = LineSpacingRuleValues.Auto }))));
    }

    private static Paragraph Paragraph(LetterBlock block)
    {
        var props = new ParagraphProperties();
        var size = block.Kind switch { LetterBlockKind.Heading1 => 36, LetterBlockKind.Heading2 => 30, LetterBlockKind.Heading3 => 25, _ => 22 };
        var heading = block.Kind is LetterBlockKind.Heading1 or LetterBlockKind.Heading2 or LetterBlockKind.Heading3;
        if (heading) props.Append(new KeepNext(), new SpacingBetweenLines { Before = "160", After = "80" });
        if (block.Kind == LetterBlockKind.Rule)
            props.Append(new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "808080", Space = 1 }));
        if (block.Kind is LetterBlockKind.Bullet or LetterBlockKind.Numbered)
            props.Append(new Indentation { Left = "567", Hanging = "283" });
        var justification = block.Align switch
        {
            LetterAlign.Center => JustificationValues.Center,
            LetterAlign.Right => JustificationValues.Right,
            LetterAlign.Justify => JustificationValues.Both,
            _ => (JustificationValues?)null
        };
        if (justification is { } j) props.Append(new Justification { Val = j });

        var paragraph = new Paragraph(props);
        if (block.Kind == LetterBlockKind.Bullet) paragraph.Append(Run("•\t", size));
        if (block.Kind == LetterBlockKind.Numbered) paragraph.Append(Run($"{block.Number}.\t", size));
        foreach (var run in block.Runs)
        {
            if (run.Break)
            {
                paragraph.Append(new Run(new Break()));
                continue;
            }
            paragraph.Append(Run(run.Text, size, run.Bold || heading, run.Italic, run.Underline));
        }
        return paragraph;
    }

    private static Run Run(string text, int size = 22, bool bold = false, bool italic = false, bool underline = false, string? color = null)
    {
        var props = new RunProperties();
        if (bold) props.Append(new Bold());
        if (italic) props.Append(new Italic());
        if (underline) props.Append(new Underline { Val = UnderlineValues.Single });
        if (color is not null) props.Append(new Color { Val = color });
        props.Append(new FontSize { Val = size.ToString() });
        return new Run(props, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static Header Header(HeaderPart part, LetterheadContent letterhead)
    {
        var lines = (letterhead.HeaderText ?? "").Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
        var text = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "0", Line = "260", LineRule = LineSpacingRuleValues.Auto }));
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0) text.Append(new Run(new Break()));
            text.Append(i == 0 ? Run(lines[i], 26, bold: true) : Run(lines[i], 17, color: "595959"));
        }

        // Logo i tekst obok siebie: niewidoczna tabela, linia pod nagłówkiem.
        var noBorders = new TableBorders(
            new TopBorder { Val = BorderValues.None }, new LeftBorder { Val = BorderValues.None }, new RightBorder { Val = BorderValues.None },
            new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "BFBFBF" },
            new InsideHorizontalBorder { Val = BorderValues.None }, new InsideVerticalBorder { Val = BorderValues.None });
        var table = new Table(new TableProperties(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }, noBorders,
            new TableLayout { Type = TableLayoutValues.Fixed }));
        var row = new TableRow();
        if (letterhead.Logo is { } logo && ImageType(logo) is { } type)
        {
            var image = part.AddImagePart(type);
            using (var ms = new MemoryStream(logo)) image.FeedData(ms);
            row.Append(new TableCell(
                new TableCellProperties(new TableCellWidth { Width = "1300", Type = TableWidthUnitValues.Dxa }, new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }),
                new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "80" }), new Run(Picture(part.GetIdOfPart(image), 700000, 700000)))));
        }
        row.Append(new TableCell(new TableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }), text));
        table.Append(row);
        return new Header(table, new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "120" })));
    }

    private static PartTypeInfo? ImageType(byte[] bytes) =>
        bytes.Length > 4 && bytes[0] == 0x89 && bytes[1] == 0x50 ? ImagePartType.Png
        : bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 ? ImagePartType.Jpeg
        : null;

    private static Drawing Picture(string relationshipId, long cx, long cy) =>
        new(new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = 1U, Name = "Logo" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties { Id = 0U, Name = "logo" }, new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
}
