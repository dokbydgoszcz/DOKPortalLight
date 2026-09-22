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
    public async Task<ActionResult<PagedResult<DokCaseDto>>> Search(
        [FromQuery] DokPath? path, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
        => Ok(await _dokCaseService.SearchAsync(path, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DokCaseDto>> GetById(Guid id, CancellationToken ct)
    {
        var dokCase = await _dokCaseService.GetByIdAsync(id, ct);
        return dokCase is null ? NotFound() : Ok(dokCase);
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK}")]
    public async Task<ActionResult<DokCaseDto>> Create(CreateDokCaseRequest request, CancellationToken ct)
    {
        var created = await _dokCaseService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK}")]
    public async Task<ActionResult<DokCaseDto>> Update(Guid id, UpdateDokCaseRequest request, CancellationToken ct)
    {
        var updated = await _dokCaseService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
