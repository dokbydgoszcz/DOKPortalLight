using DokPortal.Api.Filters;
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
        [FromQuery] DokStage? stage = null, [FromQuery] DokCaseStatusFilter? status = null, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        return Ok(await _dokCaseService.SearchAsync(path, page, pageSize, ct, stage, status));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.DokCasesView)]
    public async Task<ActionResult<DokCaseDto>> GetById(Guid id, CancellationToken ct)
    {
        var dokCase = await _dokCaseService.GetByIdAsync(id, ct);
        return dokCase is null ? NotFound() : Ok(dokCase);
    }

    [HttpPost]
    [Audit("CreateDokCase")]
    [HasPermission(Permissions.DokCasesManage)]
    public async Task<ActionResult<DokCaseDto>> Create(CreateDokCaseRequest request, CancellationToken ct)
    {
        var created = await _dokCaseService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Audit("UpdateDokCase")]
    [HasPermission(Permissions.DokCasesManage)]
    public async Task<ActionResult<DokCaseDto>> Update(Guid id, UpdateDokCaseRequest request, CancellationToken ct)
    {
        var actor = new DokCaseActor(
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value,
            User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value ?? "");
        var updated = await _dokCaseService.UpdateAsync(id, request, ct, actor);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Podopieczny zrezygnował lub przerwał formację.</summary>
    [HttpPost("{id:guid}/interrupt")]
    [Audit("InterruptDokCase")]
    [HasPermission(Permissions.DokCasesManage)]
    public async Task<ActionResult<DokCaseDto>> Interrupt(Guid id, InterruptDokCaseRequest request, CancellationToken ct)
    {
        var updated = await _dokCaseService.InterruptAsync(id, request.Date, request.Reason, ct, Actor());
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:guid}/resume")]
    [Audit("ResumeDokCase")]
    [HasPermission(Permissions.DokCasesManage)]
    public async Task<ActionResult<DokCaseDto>> Resume(Guid id, CancellationToken ct)
    {
        var updated = await _dokCaseService.ResumeAsync(id, ct, Actor());
        return updated is null ? NotFound() : Ok(updated);
    }

    private DokCaseActor Actor() => new(
        User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value,
        User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value ?? "");

    [HttpGet("{id:guid}/history")]
    [HasPermission(Permissions.DokCasesView)]
    public async Task<ActionResult<IReadOnlyList<DokCaseEventDto>>> History(Guid id, CancellationToken ct)
    {
        var history = await _dokCaseService.GetHistoryAsync(id, ct);
        return history is null ? NotFound() : Ok(history);
    }

    [HttpDelete("{id:guid}")]
    [Audit("DeleteDokCase")]
    [HasPermission(Permissions.DokCasesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _dokCaseService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
