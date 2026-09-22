using DokPortal.Application.CaseDocuments;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/dok-cases/{caseId:guid}/documents")]
[Authorize]
public class CaseDocumentsController : ControllerBase
{
    private readonly ICaseDocumentService _caseDocumentService;

    public CaseDocumentsController(ICaseDocumentService caseDocumentService) => _caseDocumentService = caseDocumentService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CaseDocumentDto>>> GetAll(Guid caseId, CancellationToken ct)
        => Ok(await _caseDocumentService.GetForCaseAsync(caseId, ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK},{AppRoles.KatechistaProwadzacy}")]
    public async Task<ActionResult<CaseDocumentDto>> Create(Guid caseId, CreateCaseDocumentRequest request, CancellationToken ct)
    {
        var created = await _caseDocumentService.CreateAsync(caseId, request, ct);
        return CreatedAtAction(nameof(GetAll), new { caseId }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK},{AppRoles.KatechistaProwadzacy}")]
    public async Task<ActionResult<CaseDocumentDto>> SetProvided(Guid caseId, Guid id, SetCaseDocumentProvidedRequest request, CancellationToken ct)
    {
        var updated = await _caseDocumentService.SetProvidedAsync(caseId, id, request.IsProvided, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
