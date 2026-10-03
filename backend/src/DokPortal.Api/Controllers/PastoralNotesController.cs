using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
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

    public PastoralNotesController(
        IPastoralNoteService pastoralNoteService, IDokCaseService dokCaseService,
        IAuditLogService auditLogService, IAuthorizationService authorization)
    {
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

    private string GetCurrentUserId() =>
        User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;

    private string GetCurrentUserEmail() =>
        User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value;
}
