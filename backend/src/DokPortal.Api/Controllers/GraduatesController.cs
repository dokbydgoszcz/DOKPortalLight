using DokPortal.Api.Authorization;
using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/graduates")]
[Authorize]
public class GraduatesController : ControllerBase
{
    private readonly IDokCaseService _dokCaseService;

    public GraduatesController(IDokCaseService dokCaseService) => _dokCaseService = dokCaseService;

    [HttpGet]
    [HasPermission(Permissions.GraduatesView)]
    public async Task<ActionResult<PagedResult<DokCaseDto>>> Search(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _dokCaseService.SearchGraduatesAsync(search, page, pageSize, ct));
}
