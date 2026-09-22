using DokPortal.Application.ParishNeeds;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/parish-needs")]
[Authorize]
public class ParishNeedsController : ControllerBase
{
    private readonly IParishNeedService _parishNeedService;

    public ParishNeedsController(IParishNeedService parishNeedService) => _parishNeedService = parishNeedService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ParishNeedDto>>> GetAll(CancellationToken ct)
        => Ok(await _parishNeedService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<ParishNeedDto>> Create(CreateParishNeedRequest request, CancellationToken ct)
    {
        var created = await _parishNeedService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpPut("{id:guid}/assign")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<ParishNeedDto>> Assign(Guid id, AssignParishNeedRequest request, CancellationToken ct)
    {
        var updated = await _parishNeedService.AssignAsync(id, request.PersonId, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
