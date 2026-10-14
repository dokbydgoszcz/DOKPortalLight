namespace DokPortal.Application.Meetings;

/// <summary>Prywatny adres kalendarza (iCal) ze spotkaniami użytkownika – do subskrypcji w Google Calendar, Outlooku, iPhonie.</summary>
public interface ICalendarFeedService
{
    /// <summary>Obecny klucz adresu (null = adres wyłączony).</summary>
    Task<string?> GetTokenAsync(string userId, CancellationToken ct);
    /// <summary>Tworzy nowy klucz; poprzedni adres przestaje działać.</summary>
    Task<string> RegenerateAsync(string userId, CancellationToken ct);
    Task DisableAsync(string userId, CancellationToken ct);
    /// <summary>Plik .ics ze spotkaniami widocznymi dla właściciela klucza; null = zły klucz, konto wyłączone lub bez dostępu do spotkań.</summary>
    Task<string?> BuildIcsAsync(string token, CancellationToken ct);
}
