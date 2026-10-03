namespace DokPortal.Application.Documents;

public class DocumentDownload
{
    public required byte[] PdfBytes { get; init; }
    public required string FileName { get; init; }
    /// <summary>Plik odtworzony z aktualnych danych (brak zapisanego egzemplarza), a nie oryginał.</summary>
    public required bool Restored { get; init; }
}
