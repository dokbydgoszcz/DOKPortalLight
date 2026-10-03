using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.Attachments;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Common;
using DokPortal.Application.Missions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/missions")]
[Authorize]
public class MissionsController : ControllerBase
{
    private readonly IMissionService _missionService;
    private readonly IAttachmentService _attachmentService;
    private readonly IAuditLogService _auditLogService;

    public MissionsController(IMissionService missionService, IAttachmentService attachmentService, IAuditLogService auditLogService)
    {
        _missionService = missionService;
        _attachmentService = attachmentService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    [HasPermission(Permissions.MissionsView)]
    public async Task<ActionResult<PagedResult<MissionDto>>> Search(
        [FromQuery] string? query, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
        => Ok(await _missionService.SearchAsync(query, page, pageSize, ct));

    [HttpGet("pending")]
    [HasPermission(Permissions.MissionsView)]
    public async Task<ActionResult<IReadOnlyList<PendingCatechistDto>>> GetPending(CancellationToken ct)
        => Ok(await _missionService.GetPendingAsync(ct));

    [HttpPost("grant")]
    [HasPermission(Permissions.MissionsManage)]
    public async Task<ActionResult<MissionDto>> Grant(GrantMissionRequest request, CancellationToken ct)
    {
        var mission = await _missionService.GrantAsync(request.PersonId, ct);
        await _auditLogService.LogAsync(
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value,
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value,
            "GrantMission", mission.PersonFullName, AuditResult.Allowed, ct);
        return Ok(mission);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.MissionsView)]
    public async Task<ActionResult<MissionDto>> GetById(Guid id, CancellationToken ct)
    {
        var mission = await _missionService.GetByIdAsync(id, ct);
        return mission is null ? NotFound() : Ok(mission);
    }

    [HttpPost]
    [HasPermission(Permissions.MissionsManage)]
    public async Task<ActionResult<MissionDto>> Create(CreateMissionRequest request, CancellationToken ct)
    {
        var created = await _missionService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.MissionsManage)]
    public async Task<ActionResult<MissionDto>> Update(Guid id, UpdateMissionRequest request, CancellationToken ct)
    {
        var updated = await _missionService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.MissionsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _missionService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/attachments")]
    [HasPermission(Permissions.MissionsManage)]
    [RequestSizeLimit(AttachmentRules.MaxRequestBytes)]
    public async Task<ActionResult<AttachmentDto>> AddAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        if (await _missionService.GetByIdAsync(id, ct) is null) return NotFound();

        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        await using var stream = file.OpenReadStream();
        return Ok(await _attachmentService.AddAsync(AttachmentOwnerType.Mission, id, stream, file.FileName, file.Length, currentUserId, ct));
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
    [HasPermission(Permissions.MissionsView)]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        if (await _missionService.GetByIdAsync(id, ct) is null) return NotFound();

        var result = await _attachmentService.DownloadAsync(AttachmentOwnerType.Mission, id, attachmentId, ct);
        if (result is null) return NotFound();

        var (file, fileName) = result.Value;
        return File(file.Content, file.ContentType, fileName);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [HasPermission(Permissions.MissionsManage)]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        if (await _missionService.GetByIdAsync(id, ct) is null) return NotFound();

        var deleted = await _attachmentService.DeleteAsync(AttachmentOwnerType.Mission, id, attachmentId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
