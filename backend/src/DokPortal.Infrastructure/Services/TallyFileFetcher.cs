using DokPortal.Application.FormSubmissions;

namespace DokPortal.Infrastructure.Services;

/// <summary>Pobiera pliki zgłoszeń z serwera plików Tally – tylko z jego domeny (adres pochodzi z danych zgłoszenia).</summary>
public class TallyFileFetcher : IFormFileFetcher
{
    public const long MaxBytes = 20 * 1024 * 1024;
    private readonly HttpClient _http;

    public TallyFileFetcher(HttpClient http) => _http = http;

    public static bool IsAllowed(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.Equals("tally.so", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".tally.so", StringComparison.OrdinalIgnoreCase));

    public async Task<DownloadedFile> FetchAsync(FormSubmissionFile file, CancellationToken ct)
    {
        if (!IsAllowed(file.Url)) throw new InvalidOperationException("Plik nie pochodzi z Tally – nie pobieram go.");
        using var response = await _http.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Tally nie oddał pliku (link mógł wygasnąć) – pobierz go ręcznie z panelu Tally.");
        if (response.Content.Headers.ContentLength > MaxBytes) throw new InvalidOperationException("Plik jest za duży (ponad 20 MB).");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes) throw new InvalidOperationException("Plik jest za duży (ponad 20 MB).");
            buffer.Write(chunk, 0, read);
        }
        var type = file.MimeType ?? response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new DownloadedFile(buffer.ToArray(), type, file.Name);
    }
}
