namespace DokPortal.Application.Documents;

public class GeneratedDocumentResult
{
    public required byte[] PdfBytes { get; init; }
    public required GeneratedDocumentDto History { get; init; }
}
