using System.IdentityModel.Tokens.Jwt;
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

    public PastoralNotesController(IPastoralNoteService pastoralNoteService, IDokCaseService dokCaseService, IAuditLogService auditLogService)
    {
        _pastoralNoteService = pastoralNoteService;
        _dokCaseService = dokCaseService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PastoralNoteDto>>> GetAll(Guid caseId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var isPrivileged = User.IsInRole(AppRoles.Administrator) || User.IsInRole(AppRoles.DyrektorDOK);
        var notes = await _pastoralNoteService.GetVisibleForCaseAsync(caseId, currentUserId, isPrivileged, ct);

        var dokCase = await _dokCaseService.GetByIdAsync(caseId, ct);
        var objectDescription = dokCase?.PersonFullName ?? $"Sprawa {caseId}";
        var isBlocked = !isPrivileged && await _pastoralNoteService.HasNotesFromOthersAsync(caseId, currentUserId, ct);
        await _auditLogService.LogAsync(
            currentUserId, GetCurrentUserEmail(), "ReadPastoralNotes", objectDescription,
            isBlocked ? AuditResult.Blocked : AuditResult.Allowed, ct);

        return Ok(notes);
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK},{AppRoles.KatechistaProwadzacy}")]
    public async Task<ActionResult<PastoralNoteDto>> Create(Guid caseId, CreatePastoralNoteRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var created = await _pastoralNoteService.CreateAsync(caseId, currentUserId, request, ct);
        return CreatedAtAction(nameof(GetAll), new { caseId }, created);
    }

    private string GetCurrentUserId() =>
        User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;

    private string GetCurrentUserEmail() =>
        User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value;
}
