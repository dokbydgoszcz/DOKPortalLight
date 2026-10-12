using DokPortal.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Api;

/// <summary>Odporność na usypianie darmowej bazy i pomocnicze funkcje ochrony przed nadużyciami.</summary>
public static class DatabaseResilience
{
    private const int MinConnectTimeoutSeconds = 60;

    /// <summary>Wydłuża czas łączenia (budzenie uśpionej bazy trwa do ok. minuty), nie ruszając reszty ustawień.</summary>
    public static string? WithConnectTimeout(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return connectionString;
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (builder.ConnectTimeout < MinConnectTimeoutSeconds) builder.ConnectTimeout = MinConnectTimeoutSeconds;
            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            return connectionString;
        }
    }

    /// <summary>Migracje przy starcie: kilka prób z odstępem, gdy baza dopiero się budzi.</summary>
    public static async Task MigrateWithRetryAsync(AppDbContext db, ILogger logger, int attempts = 5)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                return;
            }
            catch (Exception ex) when (attempt < attempts && ex is SqlException or TimeoutException or Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException)
            {
                var delay = TimeSpan.FromSeconds(15 * attempt);
                logger.LogWarning(ex, "Baza niedostępna przy starcie (próba {Attempt}/{Attempts}); ponawiam za {Delay}", attempt, attempts, delay);
                await Task.Delay(delay);
            }
        }
    }

    /// <summary>
    /// Adres klienta do limitów: za proxy App Service prawdziwy adres jest ostatnim wpisem X-Forwarded-For
    /// (wcześniejsze wpisy może podać sam klient).
    /// </summary>
    public static string ClientAddress(HttpContext http)
    {
        var forwarded = http.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var last = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
            if (!string.IsNullOrEmpty(last)) return last;
        }
        return http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
