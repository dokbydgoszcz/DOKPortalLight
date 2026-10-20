using DokPortal.Domain.Enums;

namespace DokPortal.Application.FormSubmissions;

public sealed record FormSubmissionFile(string Name, string Url, string? MimeType, long? Size);

/// <summary>Jedna odpowiedź z formularza: tekst albo pliki.</summary>
public sealed record FormSubmissionField(string Label, string? Type, string? Text, IReadOnlyList<FormSubmissionFile>? Files = null);

/// <summary>Dane osoby odczytane z odpowiedzi (z poprawkami: spacje, kod pocztowy, ul., imieniny słownie itd.).</summary>
public sealed record PersonDraft
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? AddressStreet { get; init; }
    public string? AddressPostalCode { get; init; }
    public string? AddressCity { get; init; }
    public int? NameDayDay { get; init; }
    public int? NameDayMonth { get; init; }
    /// <summary>Indeks pliku-zdjęcia na liście plików zgłoszenia (pierwszy obraz JPG/PNG/WEBP).</summary>
    public int? PhotoIndex { get; init; }
}

/// <summary>Istniejąca osoba podobna do zgłaszającej – kandydat do aktualizacji.</summary>
public sealed record FormSubmissionCandidateDto
{
    public Guid PersonId { get; init; }
    public required string FullName { get; init; }
    /// <summary>Po czym dopasowano: „e-mail”, „telefon”, „imię i nazwisko”.</summary>
    public required string MatchedBy { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? AddressStreet { get; init; }
    public string? AddressPostalCode { get; init; }
    public string? AddressCity { get; init; }
    public int? NameDayDay { get; init; }
    public int? NameDayMonth { get; init; }
    public bool HasPhoto { get; init; }
    public IReadOnlyList<string> Functions { get; init; } = Array.Empty<string>();
}

public sealed record FormSubmissionDto
{
    public Guid Id { get; init; }
    public required string Source { get; init; }
    public string? FormName { get; init; }
    public DateTime SubmittedAtUtc { get; init; }
    public FormSubmissionStatus Status { get; init; }
    public IReadOnlyList<FormSubmissionField> Fields { get; init; } = Array.Empty<FormSubmissionField>();
    /// <summary>Wszystkie pliki zgłoszenia po kolei (indeksy do pobierania).</summary>
    public IReadOnlyList<FormSubmissionFile> Files { get; init; } = Array.Empty<FormSubmissionFile>();
    public required PersonDraft Draft { get; init; }
    public IReadOnlyList<FormSubmissionCandidateDto> Candidates { get; init; } = Array.Empty<FormSubmissionCandidateDto>();
    public Guid? PersonId { get; init; }
    public string? PersonFullName { get; init; }
    public string? ReviewedByEmail { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
}

/// <summary>Zastosowanie zgłoszenia: PersonId puste = nowa osoba; inaczej aktualizacja tej osoby (puste pola nie kasują danych).</summary>
public sealed record ApplyFormSubmissionRequest
{
    public Guid? PersonId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? AddressStreet { get; init; }
    public string? AddressPostalCode { get; init; }
    public string? AddressCity { get; init; }
    public int? NameDayDay { get; init; }
    public int? NameDayMonth { get; init; }
}

public enum TallyReceiveResult
{
    Created,
    Duplicate,
    Ignored
}

public interface IFormSubmissionService
{
    /// <summary>Przyjmuje zdarzenie webhooka Tally (podpis już sprawdzony). Nieznane zdarzenia są ignorowane.</summary>
    Task<TallyReceiveResult> ReceiveTallyAsync(string json, CancellationToken ct);
    Task<IReadOnlyList<FormSubmissionDto>> ListAsync(FormSubmissionStatus status, CancellationToken ct);
    Task<int> PendingCountAsync(CancellationToken ct);
    Task<FormSubmissionDto?> ApplyAsync(Guid id, ApplyFormSubmissionRequest request, string reviewerEmail, CancellationToken ct);
    Task<FormSubmissionDto?> RejectAsync(Guid id, string reviewerEmail, CancellationToken ct);
    /// <summary>Przywraca odrzucone zgłoszenie do przejrzenia.</summary>
    Task<FormSubmissionDto?> ReopenAsync(Guid id, CancellationToken ct);
    /// <summary>Pobiera plik zgłoszenia (np. zdjęcie) z Tally; null = brak zgłoszenia lub pliku.</summary>
    Task<DownloadedFile?> GetFileAsync(Guid id, int index, CancellationToken ct);
}

public sealed record DownloadedFile(byte[] Content, string ContentType, string Name);

/// <summary>Pobiera pliki zgłoszeń z serwera formularzy (tylko zaufane adresy).</summary>
public interface IFormFileFetcher
{
    Task<DownloadedFile> FetchAsync(FormSubmissionFile file, CancellationToken ct);
}
