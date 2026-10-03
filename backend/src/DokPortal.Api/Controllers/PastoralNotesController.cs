using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
using DokPortal.Application.Attachments;
using DokPortal.Application.AuditLog;
using DokPortal.Application.DokCases;
using DokPortal.Application.PastoralNotes;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/dok-cases/{caseId:guid}/notes")]
[Authorize]
public class PastoralNotesController : ControllerBase
{
    private readonly IPastoralNoteService _pastoralNoteService;
    private readonly IDokCaseService _dokCaseService;
    private readonly IAuditLogService _auditLogService;
    private readonly IAuthorizationService _authorization;
    private readonly IAttachmentService _attachmentService;

    public PastoralNotesController(
        IPastoralNoteService pastoralNoteService, IDokCaseService dokCaseService,
        IAuditLogService auditLogService, IAuthorizationService authorization, IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
        _pastoralNoteService = pastoralNoteService;
        _dokCaseService = dokCaseService;
        _auditLogService = auditLogService;
        _authorization = authorization;
    }

    [HttpGet]
    [HasPermission(Permissions.PastoralNotesView)]
    public async Task<ActionResult<IReadOnlyList<PastoralNoteDto>>> GetAll(Guid caseId, CancellationToken ct)
    {
        var dokCase = await _dokCaseService.GetByIdAsync(caseId, ct);
        if (dokCase is null) return NotFound();

        var currentUserId = GetCurrentUserId();
        var isPrivileged = (await _authorization.AuthorizeAsync(User, null, Permissions.PastoralNotesReadAll)).Succeeded;
        var notes = await _pastoralNoteService.GetVisibleForCaseAsync(caseId, currentUserId, isPrivileged, ct);

        var objectDescription = dokCase.PersonFullName;
        var isBlocked = !isPrivileged && await _pastoralNoteService.HasNotesFromOthersAsync(caseId, currentUserId, ct);
        await _auditLogService.LogAsync(
            currentUserId, GetCurrentUserEmail(), "ReadPastoralNotes", objectDescription,
            isBlocked ? AuditResult.Blocked : AuditResult.Allowed, ct);

        return Ok(notes);
    }

    [HttpPost]
    [HasPermission(Permissions.PastoralNotesWrite)]
    public async Task<ActionResult<PastoralNoteDto>> Create(Guid caseId, CreatePastoralNoteRequest request, CancellationToken ct)
    {
        if (await _dokCaseService.GetByIdAsync(caseId, ct) is null) return NotFound();

        var currentUserId = GetCurrentUserId();
        var created = await _pastoralNoteService.CreateAsync(caseId, currentUserId, request, ct);
        return CreatedAtAction(nameof(GetAll), new { caseId }, created);
    }

    [HttpPost("{noteId:guid}/attachments")]
    [HasPermission(Permissions.PastoralNotesWrite)]
    [RequestSizeLimit(AttachmentRules.MaxRequestBytes)]
    public async Task<ActionResult<AttachmentDto>> AddAttachment(Guid caseId, Guid noteId, IFormFile file, CancellationToken ct)
    {
        if (await FindVisibleNoteCaseAsync(caseId, noteId, ct) is null) return NotFound();

        await using var stream = file.OpenReadStream();
        var attachment = await _attachmentService.AddAsync(
            AttachmentOwnerType.PastoralNote, noteId, stream, file.FileName, file.Length, GetCurrentUserId(), ct);
        return Ok(attachment);
    }

    [HttpGet("{noteId:guid}/attachments/{attachmentId:guid}/download")]
    [HasPermission(Permissions.PastoralNotesView)]
    public async Task<IActionResult> DownloadAttachment(Guid caseId, Guid noteId, Guid attachmentId, CancellationToken ct)
    {
        var dokCase = await FindVisibleNoteCaseAsync(caseId, noteId, ct, auditAction: "DownloadPastoralNoteAttachment");
        if (dokCase is null) return NotFound();

        var result = await _attachmentService.DownloadAsync(AttachmentOwnerType.PastoralNote, noteId, attachmentId, ct);
        if (result is null) return NotFound();

        await _auditLogService.LogAsync(
            GetCurrentUserId(), GetCurrentUserEmail(), "DownloadPastoralNoteAttachment", dokCase.PersonFullName, AuditResult.Allowed, ct);
        var (file, fileName) = result.Value;
        return File(file.Content, file.ContentType, fileName);
    }

    [HttpDelete("{noteId:guid}/attachments/{attachmentId:guid}")]
    [HasPermission(Permissions.PastoralNotesWrite)]
    public async Task<IActionResult> DeleteAttachment(Guid caseId, Guid noteId, Guid attachmentId, CancellationToken ct)
    {
        if (await FindVisibleNoteCaseAsync(caseId, noteId, ct) is null) return NotFound();

        var deleted = await _attachmentService.DeleteAsync(AttachmentOwnerType.PastoralNote, noteId, attachmentId, ct);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Sprawa musi być w zakresie użytkownika, a notatka widoczna dla niego (autor albo uprawniony do wszystkich notatek).
    /// Cudza notatka zachowuje się jak nieistniejąca; gdy podano auditAction, próba jest zapisywana jako zablokowana.
    /// </summary>
    private async Task<DokCaseDto?> FindVisibleNoteCaseAsync(Guid caseId, Guid noteId, CancellationToken ct, string? auditAction = null)
    {
        var dokCase = await _dokCaseService.GetByIdAsync(caseId, ct);
        if (dokCase is null) return null;

        var currentUserId = GetCurrentUserId();
        var isPrivileged = (await _authorization.AuthorizeAsync(User, null, Permissions.PastoralNotesReadAll)).Succeeded;
        var access = await _pastoralNoteService.GetAccessAsync(caseId, noteId, currentUserId, isPrivileged, ct);
        if (access == PastoralNoteAccess.Visible) return dokCase;

        if (access == PastoralNoteAccess.Hidden && auditAction is not null)
        {
            await _auditLogService.LogAsync(currentUserId, GetCurrentUserEmail(), auditAction, dokCase.PersonFullName, AuditResult.Blocked, ct);
        }
        return null;
    }

    private string GetCurrentUserId() =>
        User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;

    private string GetCurrentUserEmail() =>
        User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value;
}
