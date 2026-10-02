using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.Supervisions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/supervisions")]
[Authorize]
public class SupervisionsController : ControllerBase
{
    private readonly ISupervisionService _supervisionService;

    public SupervisionsController(ISupervisionService supervisionService) => _supervisionService = supervisionService;

    [HttpGet]
    [HasPermission(Permissions.SupervisionsView)]
    public async Task<ActionResult<IReadOnlyList<SupervisionDto>>> GetAll([FromQuery] Institution? institution, CancellationToken ct)
        => Ok(await _supervisionService.GetAllAsync(institution, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.SupervisionsView)]
    public async Task<ActionResult<SupervisionDto>> GetById(Guid id, CancellationToken ct)
    {
        var supervision = await _supervisionService.GetByIdAsync(id, ct);
        return supervision is null ? NotFound() : Ok(supervision);
    }

    [HttpPost]
    [HasPermission(Permissions.SupervisionsManage)]
    public async Task<ActionResult<SupervisionDto>> Create(CreateSupervisionRequest request, CancellationToken ct)
    {
        var created = await _supervisionService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.SupervisionsManage)]
    public async Task<ActionResult<SupervisionDto>> Update(Guid id, CreateSupervisionRequest request, CancellationToken ct)
    {
        var updated = await _supervisionService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.SupervisionsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _supervisionService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
