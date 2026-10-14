using DokPortal.Api.Filters;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
using DokPortal.Application.Attachments;
using DokPortal.Application.AuditLog;
using DokPortal.Infrastructure.Services;
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
    private readonly IAttachmentService _attachmentService;
    private readonly IMailingQueue _queue;

    public MailingController(IMailingService mailingService, IAuditLogService auditLogService, IAttachmentService attachmentService, IMailingQueue queue)
    {
        _queue = queue;
        _mailingService = mailingService;
        _auditLogService = auditLogService;
        _attachmentService = attachmentService;
    }

    [HttpGet]
    [HasPermission(Permissions.MailingView)]
    public async Task<ActionResult<IReadOnlyList<MailingCampaignDto>>> List(CancellationToken ct)
        => Ok(await _mailingService.ListAsync(ct));

    [HttpPost]
    [Audit("CreateCampaign")]
    [HasPermission(Permissions.MailingManage)]
    public async Task<ActionResult<MailingCampaignDto>> Create(CreateMailingCampaignRequest request, CancellationToken ct)
        => Ok(await _mailingService.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    [Audit("UpdateCampaign")]
    [HasPermission(Permissions.MailingManage)]
    public async Task<ActionResult<MailingCampaignDto>> Update(Guid id, CreateMailingCampaignRequest request, CancellationToken ct)
    {
        var updated = await _mailingService.UpdateDraftAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Adresaci zapisanej kampanii (podgląd przed wysyłką).</summary>
    [HttpGet("{id:guid}/recipients")]
    [HasPermission(Permissions.MailingView)]
    public async Task<ActionResult<IReadOnlyList<MailingRecipientDto>>> CampaignRecipients(Guid id, CancellationToken ct)
    {
        var recipients = await _mailingService.GetCampaignRecipientsAsync(id, ct);
        return recipients is null ? NotFound() : Ok(recipients);
    }

    /// <summary>Podgląd adresatów grupy jeszcze przed zapisaniem kampanii.</summary>
    [HttpGet("~/api/mailing/recipients")]
    [HasPermission(Permissions.MailingManage)]
    public async Task<ActionResult<IReadOnlyList<MailingRecipientDto>>> GroupRecipients([FromQuery] MailingGroup group, [FromQuery] Guid[]? personIds, CancellationToken ct)
        => Ok(await _mailingService.GetRecipientsAsync(group, personIds, ct));

    [HttpPost("{id:guid}/attachments")]
    [Audit("UploadCampaignAttachment")]
    [HasPermission(Permissions.MailingManage)]
    [RequestSizeLimit(AttachmentRules.MaxRequestBytes)]
    public async Task<ActionResult<AttachmentDto>> AddAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        var isDraft = await _mailingService.IsDraftAsync(id, ct);
        if (isDraft is null) return NotFound();
        if (isDraft == false) throw new InvalidOperationException("Do wysłanej kampanii nie można dodać pliku.");
        var existing = await _attachmentService.ListAsync(AttachmentOwnerType.MailingCampaign, id, ct);
        if (existing.Sum(a => a.SizeBytes) + file.Length > MailingService.MaxAttachmentsBytes)
            throw new InvalidOperationException("Załączniki kampanii mogą mieć łącznie najwyżej 15 MB (limit poczty).");

        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        await using var stream = file.OpenReadStream();
        return Ok(await _attachmentService.AddAsync(AttachmentOwnerType.MailingCampaign, id, stream, file.FileName, file.Length, currentUserId, ct));
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
    [HasPermission(Permissions.MailingView)]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var result = await _attachmentService.DownloadAsync(AttachmentOwnerType.MailingCampaign, id, attachmentId, ct);
        if (result is null) return NotFound();
        var (file, fileName) = result.Value;
        return File(file.Content, file.ContentType, fileName);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [Audit("DeleteCampaignAttachment")]
    [HasPermission(Permissions.MailingManage)]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var isDraft = await _mailingService.IsDraftAsync(id, ct);
        if (isDraft is null) return NotFound();
        if (isDraft == false) throw new InvalidOperationException("Z wysłanej kampanii nie można usunąć pliku.");
        var deleted = await _attachmentService.DeleteAsync(AttachmentOwnerType.MailingCampaign, id, attachmentId, ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    [Audit("DeleteCampaign")]
    [HasPermission(Permissions.MailingManage)]
    public async Task<IActionResult> DeleteDraft(Guid id, CancellationToken ct)
    {
        if (!await _mailingService.DeleteDraftAsync(id, ct)) return NotFound();
        // Pliki usuniętego szkicu nie są już nikomu potrzebne.
        foreach (var attachment in await _attachmentService.ListAsync(AttachmentOwnerType.MailingCampaign, id, ct))
        {
            await _attachmentService.DeleteAsync(AttachmentOwnerType.MailingCampaign, id, attachment.Id, ct);
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/send")]
    [Audit("SendCampaign")]
    [HasPermission(Permissions.MailingManage)]
    public async Task<ActionResult<MailingCampaignDto>> Send(Guid id, CancellationToken ct)
    {
        var sent = await _mailingService.SendAsync(id, ct);
        if (sent is null) return NotFound();
        if (sent.Status == CampaignStatus.Sending) _queue.Kick();
        return Ok(sent);
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
