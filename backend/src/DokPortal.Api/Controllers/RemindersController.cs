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

    public RemindersController(IReminderService reminderService, IConfiguration configuration)
    {
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
