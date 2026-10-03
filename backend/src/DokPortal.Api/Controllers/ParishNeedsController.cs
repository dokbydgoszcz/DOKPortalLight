using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.ParishNeeds;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/parish-needs")]
[Authorize]
public class ParishNeedsController : ControllerBase
{
    private readonly IParishNeedService _parishNeedService;

    public ParishNeedsController(IParishNeedService parishNeedService) => _parishNeedService = parishNeedService;

    [HttpGet]
    [HasPermission(Permissions.ParishNeedsView)]
    public async Task<ActionResult<IReadOnlyList<ParishNeedDto>>> GetAll(CancellationToken ct)
        => Ok(await _parishNeedService.GetAllAsync(ct));

    [HttpPost]
    [HasPermission(Permissions.ParishNeedsManage)]
    public async Task<ActionResult<ParishNeedDto>> Create(CreateParishNeedRequest request, CancellationToken ct)
    {
        var created = await _parishNeedService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.ParishNeedsManage)]
    public async Task<ActionResult<ParishNeedDto>> Update(Guid id, UpdateParishNeedRequest request, CancellationToken ct)
    {
        var updated = await _parishNeedService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPut("{id:guid}/assign")]
    [HasPermission(Permissions.ParishNeedsManage)]
    public async Task<ActionResult<ParishNeedDto>> Assign(Guid id, AssignParishNeedRequest request, CancellationToken ct)
    {
        var updated = await _parishNeedService.AssignAsync(id, request.PersonId, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}/assign/{personId:guid}")]
    [HasPermission(Permissions.ParishNeedsManage)]
    public async Task<ActionResult<ParishNeedDto>> Unassign(Guid id, Guid personId, CancellationToken ct)
    {
        var updated = await _parishNeedService.UnassignAsync(id, personId, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.ParishNeedsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _parishNeedService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
