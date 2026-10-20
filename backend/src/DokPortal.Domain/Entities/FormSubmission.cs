using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

/// <summary>
/// Zgłoszenie z formularza zewnętrznego (Tally) – trafia do kolejki „Zgłoszenia z formularzy”
/// i dopiero po przejrzeniu zapisuje się w osobach.
/// </summary>
public class FormSubmission
{
    public Guid Id { get; set; }
    /// <summary>Skąd przyszło – np. „Tally”.</summary>
    public required string Source { get; set; }
    /// <summary>Identyfikator zgłoszenia w systemie źródłowym (Tally submissionId) – chroni przed dublem przy ponownym wysłaniu.</summary>
    public required string ExternalId { get; set; }
    public string? FormId { get; set; }
    public string? FormName { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    /// <summary>Odpowiedzi w postaci JSON: [{ label, type, text, files: [{ name, url, mimeType, size }] }].</summary>
    public required string FieldsJson { get; set; }
    public FormSubmissionStatus Status { get; set; }
    /// <summary>Osoba, do której przeniesiono dane (po zastosowaniu).</summary>
    public Guid? PersonId { get; set; }
    public Person? Person { get; set; }
    public string? ReviewedByEmail { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
}
