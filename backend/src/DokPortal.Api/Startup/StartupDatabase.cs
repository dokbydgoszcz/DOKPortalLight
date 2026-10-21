using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;

namespace DokPortal.Api.Startup;

/// <summary>
/// Migracje i dane startowe tylko raz na wdrożoną wersję: pierwszy start nowej wersji je wykonuje i zapisuje
/// odcisk wersji; kolejne restarty tej samej wersji nie dotykają bazy (darmowa baza może dalej spać).
/// Wymuszenie (np. po odtworzeniu bazy z kopii): usunąć plik znacznika albo ustawić Startup:AlwaysMigrate=true.
/// </summary>
public static class StartupDatabase
{
    public const string StampFile = "startup-version.txt";

    /// <summary>Odcisk wersji kodu: identyfikatory skompilowanych modułów aplikacji (zmieniają się przy każdej zmianie kodu).</summary>
    public static string CurrentStamp() => string.Join(",", new[]
    {
        typeof(StartupDatabase).Assembly, typeof(AppDbContext).Assembly, typeof(Application.People.IPersonService).Assembly, typeof(Person).Assembly
    }.Select(a => a.ManifestModule.ModuleVersionId.ToString("N")));

    /// <returns>true, gdy praca została wykonana; false, gdy ta wersja już ją wykonała.</returns>
    public static async Task<bool> RunOncePerVersionAsync(LocalStateFiles state, string stamp, bool always, Func<Task> work, ILogger logger)
    {
        if (!always && state.Read(StampFile) == stamp)
        {
            logger.LogInformation("Ta wersja aplikacji ma już bazę zaktualizowaną – pomijam migracje przy starcie (baza może dalej spać).");
            return false;
        }
        try
        {
            await work();
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            // Np. darmowa baza wstrzymana do końca miesiąca: lepiej wystartować (strona, /health, czytelne 503)
            // niż wywrócić całą aplikację. Znacznik nie powstaje, więc następny start spróbuje ponownie.
            logger.LogError(ex, "Baza niedostępna przy starcie – aplikacja działa bez migracji, spróbuje przy następnym starcie.");
            return false;
        }
        state.Write(StampFile, stamp);
        return true;
    }

    public static bool IsDatabaseUnavailable(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is Microsoft.Data.SqlClient.SqlException or TimeoutException or Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException)
                return true;
        return false;
    }
}
