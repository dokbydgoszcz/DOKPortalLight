using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
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
    [HasPermission(Permissions.FormatorsView)]
    public async Task<ActionResult<IReadOnlyList<FormatorDto>>> GetAll(CancellationToken ct)
        => Ok(await _formatorService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.FormatorsView)]
    public async Task<ActionResult<FormatorDto>> GetById(Guid id, CancellationToken ct)
    {
        var formator = await _formatorService.GetByIdAsync(id, ct);
        return formator is null ? NotFound() : Ok(formator);
    }

    [HttpPost]
    [HasPermission(Permissions.FormatorsManage)]
    public async Task<ActionResult<FormatorDto>> Create(CreateFormatorRequest request, CancellationToken ct)
    {
        var created = await _formatorService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.FormatorsManage)]
    public async Task<ActionResult<FormatorDto>> Update(Guid id, CreateFormatorRequest request, CancellationToken ct)
    {
        var updated = await _formatorService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.FormatorsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _formatorService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
