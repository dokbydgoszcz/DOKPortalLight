using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class GeneratedDocument
{
    public Guid Id { get; set; }
    /// <summary>Dawny wbudowany typ pisma (stare wpisy i wzory startowe); null dla wzorów dodanych w portalu.</summary>
    public DocumentTemplate? Template { get; set; }
    /// <summary>Wzór, z którego powstało pismo.</summary>
    public Guid? LetterTemplateId { get; set; }
    /// <summary>Nazwa wzoru w chwili generowania (zmiana nazwy wzoru nie zmienia historii).</summary>
    public string? Title { get; set; }
    /// <summary>Wypełniona treść pisma w chwili generowania – z niej odtwarzamy PDF i Word, nawet gdy wzór się zmieni.</summary>
    public string? RenderedBody { get; set; }
    public bool UseLetterhead { get; set; }
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
