using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Mailing;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/mailing/campaigns")]
[Authorize]
public class MailingController : ControllerBase
{
    private readonly IMailingService _mailingService;
    private readonly IAuditLogService _auditLogService;

    public MailingController(IMailingService mailingService, IAuditLogService auditLogService)
    {
        _mailingService = mailingService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    [HasPermission(Permissions.MailingView)]
    public async Task<ActionResult<IReadOnlyList<MailingCampaignDto>>> List(CancellationToken ct)
        => Ok(await _mailingService.ListAsync(ct));

    [HttpPost]
    [HasPermission(Permissions.MailingManage)]
    public async Task<ActionResult<MailingCampaignDto>> Create(CreateMailingCampaignRequest request, CancellationToken ct)
        => Ok(await _mailingService.CreateAsync(request, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.MailingManage)]
    public async Task<IActionResult> DeleteDraft(Guid id, CancellationToken ct)
        => await _mailingService.DeleteDraftAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/send")]
    [HasPermission(Permissions.MailingManage)]
    public async Task<ActionResult<MailingCampaignDto>> Send(Guid id, CancellationToken ct)
    {
        var sent = await _mailingService.SendAsync(id, ct);
        return sent is null ? NotFound() : Ok(sent);
    }

    /// <summary>Wysyła wiadomość testową na adres zalogowanego użytkownika (sprawdzenie ustawień SMTP).</summary>
    [HttpPost("~/api/mailing/test-email")]
    [HasPermission(Permissions.MailingManage)]
    public async Task<IActionResult> SendTestEmail(CancellationToken ct)
    {
        var userId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var email = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value;

        await _mailingService.SendTestAsync(email, ct);
        await _auditLogService.LogAsync(userId, email, "SendTestEmail", email, AuditResult.Allowed, ct);
        return Ok(new { sentTo = email });
    }
}
