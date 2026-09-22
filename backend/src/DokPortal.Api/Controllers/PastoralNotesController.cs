using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.PastoralNotes;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/dok-cases/{caseId:guid}/notes")]
[Authorize]
public class PastoralNotesController : ControllerBase
{
    private readonly IPastoralNoteService _pastoralNoteService;

    public PastoralNotesController(IPastoralNoteService pastoralNoteService) => _pastoralNoteService = pastoralNoteService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PastoralNoteDto>>> GetAll(Guid caseId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var isPrivileged = User.IsInRole(AppRoles.Administrator) || User.IsInRole(AppRoles.DyrektorDOK);
        return Ok(await _pastoralNoteService.GetVisibleForCaseAsync(caseId, currentUserId, isPrivileged, ct));
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK},{AppRoles.KatechistaProwadzacy}")]
    public async Task<ActionResult<PastoralNoteDto>> Create(Guid caseId, CreatePastoralNoteRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var created = await _pastoralNoteService.CreateAsync(caseId, currentUserId, request, ct);
        return CreatedAtAction(nameof(GetAll), new { caseId }, created);
    }

    private string GetCurrentUserId() =>
        User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
}
