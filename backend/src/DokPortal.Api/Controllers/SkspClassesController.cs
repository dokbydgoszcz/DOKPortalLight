using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
using DokPortal.Api.Filters;
using DokPortal.Application.SkspClasses;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

/// <summary>Zajęcia SKŚP z listą obecności – z nich liczy się frekwencja kandydatów.</summary>
[ApiController]
[Route("api/sksp-classes")]
[Authorize]
public class SkspClassesController : ControllerBase
{
    private readonly ISkspClassService _service;

    public SkspClassesController(ISkspClassService service) => _service = service;

    [HttpGet]
    [HasPermission(Permissions.CandidatesView)]
    public async Task<ActionResult<IReadOnlyList<SkspClassDto>>> GetAll([FromQuery] int? year, CancellationToken ct) =>
        Ok(await _service.GetAllAsync(year, ct));

    [HttpPost]
    [Audit("CreateSkspClass")]
    [HasPermission(Permissions.CandidatesManage)]
    public async Task<ActionResult<SkspClassDto>> Create(SaveSkspClassRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpPut("{id:guid}")]
    [Audit("UpdateSkspClass")]
    [HasPermission(Permissions.CandidatesManage)]
    public async Task<ActionResult<SkspClassDto>> Update(Guid id, SaveSkspClassRequest request, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPut("{id:guid}/attendance")]
    [HasPermission(Permissions.CandidatesManage)]
    public async Task<ActionResult<SkspClassDto>> SetAttendance(Guid id, IReadOnlyList<SkspAttendanceItem> items, CancellationToken ct)
    {
        var updated = await _service.SetAttendanceAsync(id, items, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:guid}/refresh-attendees")]
    [HasPermission(Permissions.CandidatesManage)]
    public async Task<ActionResult<SkspClassDto>> RefreshAttendees(Guid id, CancellationToken ct)
    {
        var updated = await _service.RefreshAttendeesAsync(id, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Audit("DeleteSkspClass")]
    [HasPermission(Permissions.CandidatesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        return await _service.DeleteAsync(id, userId, ct) ? NoContent() : NotFound();
    }
}
