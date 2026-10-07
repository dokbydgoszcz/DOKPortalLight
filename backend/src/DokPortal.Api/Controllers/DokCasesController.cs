using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/dok-cases")]
[Authorize]
public class DokCasesController : ControllerBase
{
    private readonly IDokCaseService _dokCaseService;

    public DokCasesController(IDokCaseService dokCaseService) => _dokCaseService = dokCaseService;

    [HttpGet]
    [HasPermission(Permissions.DokCasesView)]
    public async Task<ActionResult<PagedResult<DokCaseDto>>> Search(
        [FromQuery] DokPath? path, [FromQuery] int page = 1, [FromQuery] int pageSize = 100,
        [FromQuery] DokStage? stage = null, CancellationToken ct = default)
        => Ok(await _dokCaseService.SearchAsync(path, page, pageSize, ct, stage));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.DokCasesView)]
    public async Task<ActionResult<DokCaseDto>> GetById(Guid id, CancellationToken ct)
    {
        var dokCase = await _dokCaseService.GetByIdAsync(id, ct);
        return dokCase is null ? NotFound() : Ok(dokCase);
    }

    [HttpPost]
    [HasPermission(Permissions.DokCasesManage)]
    public async Task<ActionResult<DokCaseDto>> Create(CreateDokCaseRequest request, CancellationToken ct)
    {
        var created = await _dokCaseService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.DokCasesManage)]
    public async Task<ActionResult<DokCaseDto>> Update(Guid id, UpdateDokCaseRequest request, CancellationToken ct)
    {
        var updated = await _dokCaseService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.DokCasesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _dokCaseService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
