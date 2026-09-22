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
    public async Task<ActionResult<IReadOnlyList<SupervisionDto>>> GetAll([FromQuery] Institution? institution, CancellationToken ct)
        => Ok(await _supervisionService.GetAllAsync(institution, ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK},{AppRoles.DyrektorSKSP},{AppRoles.Superwizor}")]
    public async Task<ActionResult<SupervisionDto>> Create(CreateSupervisionRequest request, CancellationToken ct)
    {
        var created = await _supervisionService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }
}
