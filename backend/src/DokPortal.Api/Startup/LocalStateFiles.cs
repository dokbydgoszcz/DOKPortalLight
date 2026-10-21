namespace DokPortal.Api.Startup;

/// <summary>
/// Drobne znaczniki na lokalnym, trwałym dysku App Service (katalog %HOME%, przeżywa restarty).
/// Dzięki nim zwykły restart aplikacji (uśpienie planu F1, recykling) nie musi budzić bazy, która liczy darmowy limit
/// w czasie bycia włączoną. Poza Azure (lokalnie, testy) znaczniki są wyłączone, chyba że ustawiono Startup:StateDirectory.
/// </summary>
public sealed class LocalStateFiles
{
    private readonly ILogger<LocalStateFiles>? _logger;

    public LocalStateFiles(IConfiguration configuration, ILogger<LocalStateFiles>? logger = null)
    {
        _logger = logger;
        Directory = configuration["Startup:StateDirectory"] is { Length: > 0 } configured
            ? configured
            : Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") is not null && Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } home
                ? Path.Combine(home, "data", "dokportal")
                : null;
    }

    public string? Directory { get; }

    public bool Enabled => Directory is not null;

    public string? Read(string name)
    {
        if (Directory is null) return null;
        try
        {
            var path = Path.Combine(Directory, name);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Nie udało się odczytać znacznika {Name}", name);
            return null;
        }
    }

    public bool Exists(string name) => Read(name) is not null;

    public void Write(string name, string content)
    {
        if (Directory is null) return;
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(Path.Combine(Directory, name), content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Nie udało się zapisać znacznika {Name}", name);
        }
    }

    public void Delete(string name)
    {
        if (Directory is null) return;
        try
        {
            File.Delete(Path.Combine(Directory, name));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Nie udało się usunąć znacznika {Name}", name);
        }
    }
}
