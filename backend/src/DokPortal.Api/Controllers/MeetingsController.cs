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
    public async Task<ActionResult<IReadOnlyList<MeetingDto>>> GetAll(CancellationToken ct)
        => Ok(await _meetingService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK},{AppRoles.KatechistaProwadzacy}")]
    public async Task<ActionResult<MeetingDto>> Create(CreateMeetingRequest request, CancellationToken ct)
    {
        var created = await _meetingService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }
}
