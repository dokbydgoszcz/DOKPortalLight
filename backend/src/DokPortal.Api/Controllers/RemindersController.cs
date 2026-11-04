using System.Security.Cryptography;
using System.Text;
using DokPortal.Application.Reminders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/reminders")]
public class RemindersController : ControllerBase
{
    private readonly IReminderService _reminderService;
    private readonly IConfiguration _configuration;
    private readonly DokPortal.Application.Mailing.IMailingQueue? _mailingQueue;

    private readonly DokPortal.Application.FormSubmissions.IFormSubmissionService? _formSubmissions;
    private readonly DokPortal.Application.FormSubmissions.ITallyClient? _tally;

    public RemindersController(IReminderService reminderService, IConfiguration configuration, DokPortal.Application.Mailing.IMailingQueue? mailingQueue = null,
        DokPortal.Application.FormSubmissions.IFormSubmissionService? formSubmissions = null, DokPortal.Application.FormSubmissions.ITallyClient? tally = null)
    {
        _tally = tally;
        _formSubmissions = formSubmissions;
        _mailingQueue = mailingQueue;
        _reminderService = reminderService;
        _configuration = configuration;
    }

    [HttpPost("missing-documents/run")]
    public async Task<ActionResult<MissingDocumentsReminderResultDto>> RunMissingDocumentsReminder(CancellationToken ct)
    {
        if (!IsRequestAuthorized())
        {
            return Unauthorized();
        }

        var result = await _reminderService.RunMissingDocumentsReminderAsync(ct);
        return Ok(result);
    }

    [HttpPost("upcoming-meetings/run")]
    public async Task<ActionResult<UpcomingMeetingsReminderResultDto>> RunUpcomingMeetingsReminder(CancellationToken ct)
    {
        if (!IsRequestAuthorized())
        {
            return Unauthorized();
        }

        var result = await _reminderService.RunUpcomingMeetingsReminderAsync(ct);
        return Ok(result);
    }

    [HttpPost("upcoming-name-days/run")]
    public async Task<ActionResult<UpcomingNameDaysReminderResultDto>> RunUpcomingNameDaysReminder(CancellationToken ct)
    {
        if (!IsRequestAuthorized())
        {
            return Unauthorized();
        }

        var result = await _reminderService.RunUpcomingNameDaysReminderAsync(ct);
        return Ok(result);
    }

    [HttpPost("expiring-missions/run")]
    public async Task<ActionResult<ExpiringMissionsReminderResultDto>> RunExpiringMissionsReminder(CancellationToken ct)
    {
        if (!IsRequestAuthorized())
        {
            return Unauthorized();
        }

        return Ok(await _reminderService.RunExpiringMissionsReminderAsync(ct));
    }

    /// <summary>Codzienne wznowienie kolejki mailingu (po uśpieniu aplikacji); wysyłka idzie w tle.</summary>
    [HttpPost("mailing-queue/run")]
    public IActionResult RunMailingQueue()
    {
        if (!IsRequestAuthorized())
        {
            return Unauthorized();
        }

        _mailingQueue?.Kick();
        return Accepted();
    }

    /// <summary>Codzienne uzupełnienie kolejki zgłoszeń o to, co nie doszło z Tally webhookiem.</summary>
    [HttpPost("tally-sync/run")]
    public async Task<IActionResult> RunTallySync(CancellationToken ct)
    {
        if (!IsRequestAuthorized())
        {
            return Unauthorized();
        }

        // Bez klucza API to nie błąd dnia – zadanie po prostu nic nie robi (webhook dalej działa).
        if (_tally is not { IsConfigured: true }) return Ok(new { skipped = "Brak Tally__ApiKey – pobieranie z Tally wyłączone." });
        return Ok(await _formSubmissions!.SyncTallyAsync(ct));
    }

    private bool IsRequestAuthorized()
    {
        var configuredKey = _configuration["Reminders:ApiKey"];
        return !string.IsNullOrEmpty(configuredKey) && HasValidKey(Request.Headers["X-Reminders-Key"], configuredKey);
    }

    private static bool HasValidKey(StringValues provided, string configured)
    {
        if (provided.Count != 1 || string.IsNullOrEmpty(provided[0]))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided[0]!);
        var configuredBytes = Encoding.UTF8.GetBytes(configured);
        return providedBytes.Length == configuredBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
