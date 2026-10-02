using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.Meetings;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/meetings")]
[Authorize]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingService _meetingService;

    public MeetingsController(IMeetingService meetingService) => _meetingService = meetingService;

    [HttpGet]
    [HasPermission(Permissions.MeetingsView)]
    public async Task<ActionResult<IReadOnlyList<MeetingDto>>> GetAll(CancellationToken ct)
        => Ok(await _meetingService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.MeetingsView)]
    public async Task<ActionResult<MeetingDto>> GetById(Guid id, CancellationToken ct)
    {
        var meeting = await _meetingService.GetByIdAsync(id, ct);
        return meeting is null ? NotFound() : Ok(meeting);
    }

    [HttpPost]
    [HasPermission(Permissions.MeetingsManage)]
    public async Task<ActionResult<MeetingDto>> Create(CreateMeetingRequest request, CancellationToken ct)
    {
        var created = await _meetingService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.MeetingsManage)]
    public async Task<ActionResult<MeetingDto>> Update(Guid id, CreateMeetingRequest request, CancellationToken ct)
    {
        var updated = await _meetingService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.MeetingsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _meetingService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
