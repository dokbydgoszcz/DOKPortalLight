using DokPortal.Application.Formators;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/formators")]
[Authorize]
public class FormatorsController : ControllerBase
{
    private readonly IFormatorService _formatorService;

    public FormatorsController(IFormatorService formatorService) => _formatorService = formatorService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FormatorDto>>> GetAll(CancellationToken ct)
        => Ok(await _formatorService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<FormatorDto>> Create(CreateFormatorRequest request, CancellationToken ct)
    {
        var created = await _formatorService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }
}
