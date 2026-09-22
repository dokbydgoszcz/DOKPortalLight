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
    public async Task<ActionResult<PagedResult<MissionDto>>> Search(
        [FromQuery] string? query, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
        => Ok(await _missionService.SearchAsync(query, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MissionDto>> GetById(Guid id, CancellationToken ct)
    {
        var mission = await _missionService.GetByIdAsync(id, ct);
        return mission is null ? NotFound() : Ok(mission);
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<MissionDto>> Create(CreateMissionRequest request, CancellationToken ct)
    {
        var created = await _missionService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<MissionDto>> Update(Guid id, UpdateMissionRequest request, CancellationToken ct)
    {
        var updated = await _missionService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
