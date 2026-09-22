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
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<ActionResult<ParishDto>> Create(CreateParishRequest request, CancellationToken ct)
    {
        var created = await _parishService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }
}
