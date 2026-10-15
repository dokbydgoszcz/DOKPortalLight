using DokPortal.Api.Filters;
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
    private readonly IParishImportService? _import;

    public ParishesController(IParishService parishService, IParishImportService? import = null)
    {
        _parishService = parishService;
        _import = import;
    }

    /// <summary>Podgląd importu spisu parafii z proboszczami (nic nie zapisuje).</summary>
    [HttpPost("import/preview")]
    [HasPermission(Permissions.ParishesManage)]
    [HasPermission(Permissions.PeopleManage)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ParishImportResultDto>> ImportPreview(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await _import!.RunAsync(stream, apply: false, ct));
    }

    /// <summary>Import spisu parafii z proboszczami: tworzy brakujące parafie i osoby, przypisuje proboszczów.</summary>
    [HttpPost("import")]
    [Audit("ImportParishes")]
    [HasPermission(Permissions.ParishesManage)]
    [HasPermission(Permissions.PeopleManage)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ParishImportResultDto>> Import(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await _import!.RunAsync(stream, apply: true, ct));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ParishDto>>> GetAll(CancellationToken ct)
        => Ok(await _parishService.GetAllAsync(ct));

    [HttpPost]
    [Audit("CreateParish")]
    [HasPermission(Permissions.ParishesManage)]
    public async Task<ActionResult<ParishDto>> Create(CreateParishRequest request, CancellationToken ct)
    {
        var created = await _parishService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpPut("{id:guid}")]
    [Audit("UpdateParish")]
    [HasPermission(Permissions.ParishesManage)]
    public async Task<ActionResult<ParishDto>> Update(Guid id, UpdateParishRequest request, CancellationToken ct)
    {
        var updated = await _parishService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Audit("DeleteParish")]
    [HasPermission(Permissions.ParishesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _parishService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
