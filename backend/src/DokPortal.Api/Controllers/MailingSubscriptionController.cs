using DokPortal.Application.Mailing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DokPortal.Api.Controllers;

/// <summary>Link „wypisz się” ze stopki kampanii – bez logowania, klucz w adresie.</summary>
[ApiController]
[Route("api/mailing/subscription/{token}")]
[AllowAnonymous]
[EnableRateLimiting("login")]
public class MailingSubscriptionController : ControllerBase
{
    private readonly IMailingService _mailingService;

    public MailingSubscriptionController(IMailingService mailingService) => _mailingService = mailingService;

    [HttpGet]
    public async Task<ActionResult<MailingSubscriptionDto>> Get(string token, CancellationToken ct) =>
        await _mailingService.GetSubscriptionAsync(token, ct) is { } dto ? Ok(dto) : NotFound();

    /// <summary>Wypisuje z mailingu (POST, żeby skanery linków w poczcie nie wypisywały same).</summary>
    [HttpPost("unsubscribe")]
    public async Task<ActionResult<MailingSubscriptionDto>> Unsubscribe(string token, CancellationToken ct) =>
        await _mailingService.SetOptOutAsync(token, true, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpPost("resubscribe")]
    public async Task<ActionResult<MailingSubscriptionDto>> Resubscribe(string token, CancellationToken ct) =>
        await _mailingService.SetOptOutAsync(token, false, ct) is { } dto ? Ok(dto) : NotFound();
}
