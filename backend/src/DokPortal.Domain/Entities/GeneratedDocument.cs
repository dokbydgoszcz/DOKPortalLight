using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class GeneratedDocument
{
    public Guid Id { get; set; }
    public DocumentTemplate Template { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public required string GeneratedByUserId { get; set; }
    public string? AdditionalNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Ścieżka zapisanego egzemplarza PDF w magazynie plików; puste dla starszych wpisów lub gdy zapis się nie udał.</summary>
    public string? BlobPath { get; set; }
    public long? FileSizeBytes { get; set; }
    /// <summary>Ile razy plik został wydany (generowanie liczy się jako pierwsze pobranie).</summary>
    public int DownloadCount { get; set; } = 1;
}
