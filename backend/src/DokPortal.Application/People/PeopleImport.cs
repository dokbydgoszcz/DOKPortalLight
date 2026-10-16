using DokPortal.Domain.Enums;

namespace DokPortal.Application.People;

/// <summary>Import wklejonej listy osób (np. absolwentów rocznika) z nadaniem funkcji.</summary>
public interface IPeopleImportService
{
    /// <summary>Przy <paramref name="apply"/> = false tylko podgląd – nic nie zapisuje.</summary>
    /// <exception cref="InvalidOperationException">Pusta lista.</exception>
    Task<PeopleImportResultDto> RunAsync(PeopleImportRequest request, bool apply, CancellationToken ct);
}

public class PeopleImportRequest
{
    /// <summary>Jedna osoba w wierszu: „Nazwisko Imię” (numer na początku jest pomijany); dalej, po tabulatorze lub średniku, e-mail i telefon.</summary>
    public required string Text { get; init; }
    /// <summary>Kolejność w wierszu: true = „Imię Nazwisko”, false = „Nazwisko Imię”.</summary>
    public bool FirstNameFirst { get; init; }
    /// <summary>Funkcja do nadania (np. Katechista); brak = tylko osoby.</summary>
    public FunctionType? Function { get; init; }
    /// <summary>Notatka przy nadanej funkcji, np. „Absolwent SKŚP 2023–2025”.</summary>
    public string? FunctionNote { get; init; }
    /// <summary>Wpisz osoby jako kandydatów SKŚP. Rok formacji z nagłówków „Rok I/II/III” w tekście, a bez nagłówka – <see cref="CandidateYear"/>.</summary>
    public bool EnrollAsCandidates { get; init; }
    public int? CandidateYear { get; init; }
}

public class PeopleImportResultDto
{
    public required bool Applied { get; init; }
    public required int NewPeople { get; init; }
    public required int ExistingPeople { get; init; }
    public required int FunctionsGranted { get; init; }
    public required int CandidatesEnrolled { get; init; }
    /// <summary>Wiersze uznane za nagłówki albo nieczytelne (pominięte) – do sprawdzenia w podglądzie.</summary>
    public IReadOnlyList<string> SkippedLines { get; init; } = Array.Empty<string>();
    public required IReadOnlyList<PeopleImportRowDto> Rows { get; init; }
}

public class PeopleImportRowDto
{
    public required int Line { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public required bool NewPerson { get; init; }
    /// <summary>Osoba miała już tę funkcję.</summary>
    public required bool HadFunction { get; init; }
    /// <summary>Rok formacji SKŚP, na który osoba zostanie wpisana (null = nie wpisujemy).</summary>
    public int? CandidateYear { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
