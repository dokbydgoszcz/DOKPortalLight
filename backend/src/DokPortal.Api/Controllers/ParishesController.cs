using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.Parishes;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/parishes")]
[Authorize]
public class ParishesController : ControllerBase
{
    private readonly IParishService _parishService;

    public ParishesController(IParishService parishService) => _parishService = parishService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ParishDto>>> GetAll(CancellationToken ct)
        => Ok(await _parishService.GetAllAsync(ct));

    [HttpPost]
    [HasPermission(Permissions.ParishesManage)]
    public async Task<ActionResult<ParishDto>> Create(CreateParishRequest request, CancellationToken ct)
    {
        var created = await _parishService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.ParishesManage)]
    public async Task<ActionResult<ParishDto>> Update(Guid id, UpdateParishRequest request, CancellationToken ct)
    {
        var updated = await _parishService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.ParishesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _parishService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
