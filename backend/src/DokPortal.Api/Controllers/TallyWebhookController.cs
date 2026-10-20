using System.Text;
using DokPortal.Api.Integrations;
using DokPortal.Application.FormSubmissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

/// <summary>
/// Webhook Tally: każde nowe zgłoszenie formularza trafia do kolejki „Zgłoszenia z formularzy”.
/// Bez logowania – wiarygodność sprawdza podpis (sekret w ustawieniu Tally:SigningSecret, ten sam co w Tally).
/// </summary>
[ApiController]
[Route("api/integrations/tally/webhook")]
[AllowAnonymous]
public class TallyWebhookController : ControllerBase
{
    private const int MaxBodyBytes = 1024 * 1024;
    private readonly IFormSubmissionService _submissions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TallyWebhookController> _logger;

    public TallyWebhookController(IFormSubmissionService submissions, IConfiguration configuration, ILogger<TallyWebhookController> logger)
    {
        _submissions = submissions;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var secret = _configuration["Tally:SigningSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning("Webhook Tally odrzucony: brak ustawienia Tally:SigningSecret.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Integracja z Tally nie jest skonfigurowana." });
        }

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var body = buffer.ToArray();
        if (!TallySignature.IsValid(secret, body, Request.Headers[TallySignature.Header]))
        {
            _logger.LogWarning("Webhook Tally odrzucony: nieprawidłowy podpis.");
            return Unauthorized(new { message = "Nieprawidłowy podpis." });
        }

        var result = await _submissions.ReceiveTallyAsync(Encoding.UTF8.GetString(body), ct);
        _logger.LogInformation("Webhook Tally: {Result}.", result);
        return Ok(new { result = result.ToString() });
    }
}
