using DokPortal.Application.NameDays;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/name-days")]
[Authorize]
public class NameDaysController : ControllerBase
{
    private readonly INameDayService _nameDayService;

    public NameDaysController(INameDayService nameDayService) => _nameDayService = nameDayService;

    [HttpGet("upcoming")]
    public async Task<ActionResult<IReadOnlyList<UpcomingNameDayDto>>> GetUpcoming([FromQuery] int days, CancellationToken ct)
    {
        var window = days <= 0 ? 30 : days;
        return Ok(await _nameDayService.GetUpcomingAsync(window, ct));
    }
}
