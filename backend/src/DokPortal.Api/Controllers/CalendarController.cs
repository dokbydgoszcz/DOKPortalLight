using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
using DokPortal.Api.Filters;
using DokPortal.Application.Meetings;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

/// <summary>Prywatny adres kalendarza (iCal) – subskrypcja spotkań w Google Calendar, Outlooku, iPhonie.</summary>
[ApiController]
[Route("api/calendar")]
public class CalendarController : ControllerBase
{
    private readonly ICalendarFeedService _feed;

    public CalendarController(ICalendarFeedService feed) => _feed = feed;

    public sealed record FeedResponse(string? Url);

    private string UserId() => User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value;

    /// <summary>Adres zawsze przez HTTPS (w Azure TLS kończy się przed aplikacją, więc żądanie widzi „http”); lokalnie bez zmian.</summary>
    private string? UrlFor(string? token)
    {
        if (token is null) return null;
        var local = Request.Host.Host is "localhost" or "127.0.0.1";
        var scheme = local ? Request.Scheme : "https";
        return $"{scheme}://{Request.Host}{Request.PathBase}/api/calendar/{token}.ics";
    }

    [HttpGet("feed")]
    [Authorize]
    [HasPermission(Permissions.MeetingsView)]
    public async Task<ActionResult<FeedResponse>> GetFeed(CancellationToken ct) =>
        Ok(new FeedResponse(UrlFor(await _feed.GetTokenAsync(UserId(), ct))));

    /// <summary>Tworzy (albo zmienia) adres; poprzedni przestaje działać.</summary>
    [HttpPost("feed")]
    [Authorize]
    [Audit("CreateCalendarFeed")]
    [HasPermission(Permissions.MeetingsView)]
    public async Task<ActionResult<FeedResponse>> CreateFeed(CancellationToken ct) =>
        Ok(new FeedResponse(UrlFor(await _feed.RegenerateAsync(UserId(), ct))));

    [HttpDelete("feed")]
    [Authorize]
    [Audit("DisableCalendarFeed")]
    [HasPermission(Permissions.MeetingsView)]
    public async Task<IActionResult> DisableFeed(CancellationToken ct)
    {
        await _feed.DisableAsync(UserId(), ct);
        return NoContent();
    }

    /// <summary>Plik kalendarza dla aplikacji kalendarza (bez logowania – klucz w adresie jest hasłem).</summary>
    [HttpGet("{token}.ics")]
    [AllowAnonymous]
    public async Task<IActionResult> Ics(string token, CancellationToken ct)
    {
        var ics = await _feed.BuildIcsAsync(token, ct);
        if (ics is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=900";
        return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", "dok-portal.ics");
    }
}
