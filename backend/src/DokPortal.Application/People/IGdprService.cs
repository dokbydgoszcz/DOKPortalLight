namespace DokPortal.Application.People;

/// <summary>Prawa osoby z RODO: kopia danych (art. 15 i 20) oraz usunięcie danych (art. 17) przez anonimizację.</summary>
public interface IGdprService
{
    /// <summary>Wszystkie dane osoby zapisane w portalu, w formie do przekazania jej (JSON). Null = nie ma takiej osoby.</summary>
    Task<IReadOnlyDictionary<string, object?>?> ExportAsync(Guid personId, CancellationToken ct);

    /// <summary>
    /// Usuwa dane osobowe: imię i nazwisko zastępuje „Osoba zanonimizowana”, czyści kontakt, notatki, pliki i konto.
    /// Zostają tylko liczby do statystyk (np. że była sprawa DOK na danym etapie).
    /// </summary>
    /// <exception cref="InvalidOperationException">Osoba prowadzi otwarte sprawy (trzeba je najpierw przekazać) albo już jest zanonimizowana.</exception>
    Task<bool> AnonymizeAsync(Guid personId, CancellationToken ct);
}
