using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Candidates;
using DokPortal.Application.Common;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/candidates")]
[Authorize]
public class CandidatesController : ControllerBase
{
    private readonly ICandidateService _candidateService;
    private readonly IAuditLogService _auditLogService;

    public CandidatesController(ICandidateService candidateService, IAuditLogService auditLogService)
    {
        _candidateService = candidateService;
        _auditLogService = auditLogService;
    }

    private CandidateActor CurrentActor() => new(
        User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value,
        User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);

    [HttpGet]
    [HasPermission(Permissions.CandidatesView)]
    public async Task<ActionResult<PagedResult<CandidateDto>>> Search(
        [FromQuery] int? year, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
        => Ok(await _candidateService.SearchAsync(year, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.CandidatesView)]
    public async Task<ActionResult<CandidateDto>> GetById(Guid id, CancellationToken ct)
    {
        var candidate = await _candidateService.GetByIdAsync(id, ct);
        return candidate is null ? NotFound() : Ok(candidate);
    }

    [HttpPost]
    [HasPermission(Permissions.CandidatesManage)]
    public async Task<ActionResult<CandidateDto>> Create(CreateCandidateRequest request, CancellationToken ct)
    {
        var created = await _candidateService.CreateAsync(request, ct, CurrentActor());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.CandidatesManage)]
    public async Task<ActionResult<CandidateDto>> Update(Guid id, UpdateCandidateRequest request, CancellationToken ct)
    {
        var updated = await _candidateService.UpdateAsync(id, request, ct, CurrentActor());
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Przenosi zaznaczonych kandydatów do następnego roku (z III roku: kończy formację).</summary>
    [HttpPost("advance")]
    [HasPermission(Permissions.CandidatesManage)]
    public async Task<ActionResult<AdvanceCandidatesResultDto>> Advance(AdvanceCandidatesRequest request, CancellationToken ct)
    {
        var actor = CurrentActor();
        var result = await _candidateService.AdvanceAsync(request.CandidateIds, actor, ct);
        await _auditLogService.LogAsync(
            actor.UserId, actor.Email, "AdvanceCandidates",
            $"przeniesiono: {result.Advanced}, ukończyło formację: {result.Completed}, pominięto: {result.Skipped.Count}",
            AuditResult.Allowed, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.CandidatesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _candidateService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
