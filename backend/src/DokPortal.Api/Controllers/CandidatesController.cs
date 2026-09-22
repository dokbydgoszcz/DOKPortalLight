using DokPortal.Application.Candidates;
using DokPortal.Application.Common;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/candidates")]
[Authorize]
public class CandidatesController : ControllerBase
{
    private readonly ICandidateService _candidateService;

    public CandidatesController(ICandidateService candidateService) => _candidateService = candidateService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<CandidateDto>>> Search(
        [FromQuery] int? year, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
        => Ok(await _candidateService.SearchAsync(year, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CandidateDto>> GetById(Guid id, CancellationToken ct)
    {
        var candidate = await _candidateService.GetByIdAsync(id, ct);
        return candidate is null ? NotFound() : Ok(candidate);
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<CandidateDto>> Create(CreateCandidateRequest request, CancellationToken ct)
    {
        var created = await _candidateService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<CandidateDto>> Update(Guid id, UpdateCandidateRequest request, CancellationToken ct)
    {
        var updated = await _candidateService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
