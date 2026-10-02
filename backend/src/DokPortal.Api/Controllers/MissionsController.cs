using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.Common;
using DokPortal.Application.Missions;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/missions")]
[Authorize]
public class MissionsController : ControllerBase
{
    private readonly IMissionService _missionService;

    public MissionsController(IMissionService missionService) => _missionService = missionService;

    [HttpGet]
    [HasPermission(Permissions.MissionsView)]
    public async Task<ActionResult<PagedResult<MissionDto>>> Search(
        [FromQuery] string? query, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
        => Ok(await _missionService.SearchAsync(query, page, pageSize, ct));

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
}
