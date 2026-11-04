using System.Net.Http.Headers;
using System.Text.Json;
using DokPortal.Application.FormSubmissions;
using Microsoft.Extensions.Configuration;

namespace DokPortal.Infrastructure.Services;

/// <summary>API Tally (https://api.tally.so) – lista zgłoszeń formularza. Klucz: Tally:ApiKey (ustawia administrator w Azure).</summary>
public class TallyApiClient : ITallyClient
{
    private const int PageSize = 100;
    private const int MaxPages = 20;
    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public TallyApiClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _http.BaseAddress ??= new Uri("https://api.tally.so/");
        _apiKey = configuration["Tally:ApiKey"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<IReadOnlyList<TallyEvent>> ListSubmissionsAsync(string formId, DateTime sinceUtc, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("Brak klucza API Tally – administrator ustawia go w Azure jako Tally__ApiKey.");
        var formName = await FormNameAsync(formId, ct);
        var all = new List<TallyEvent>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var url = $"forms/{Uri.EscapeDataString(formId)}/submissions?filter=completed&limit={PageSize}&page={page}&startDate={Uri.EscapeDataString(sinceUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}";
            var json = await GetAsync(url, ct);
            all.AddRange(FormSubmissionParser.ParseTallyApi(json, formId, formName));
            using var doc = JsonDocument.Parse(json);
            if (!(doc.RootElement.TryGetProperty("hasMore", out var more) && more.ValueKind == JsonValueKind.True)) break;
        }
        return all;
    }

    private async Task<string?> FormNameAsync(string formId, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await GetAsync($"forms/{Uri.EscapeDataString(formId)}", ct));
        return doc.RootElement.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString()?.Trim() : null;
    }

    private async Task<string> GetAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "Tally odrzucił klucz API – sprawdź Tally__ApiKey w Azure.",
                System.Net.HttpStatusCode.NotFound => "Tally nie zna formularza z ustawienia Tally__FormIds.",
                _ => $"Tally nie odpowiedział poprawnie ({(int)response.StatusCode}) – spróbuj później."
            });
        return await response.Content.ReadAsStringAsync(ct);
    }
}
