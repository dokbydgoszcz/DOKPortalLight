namespace DokPortal.Application.Documents;

public class DocumentDownload
{
    public required byte[] Bytes { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    /// <summary>Plik odtworzony (z zapisanej treści albo z aktualnych danych), a nie zapisany oryginał.</summary>
    public required bool Restored { get; init; }
}
