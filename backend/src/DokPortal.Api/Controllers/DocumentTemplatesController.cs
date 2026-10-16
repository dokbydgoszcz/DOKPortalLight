using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
using DokPortal.Api.Filters;
using DokPortal.Application.Documents;
using DokPortal.Domain.Constants;
using DokPortal.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

/// <summary>Wzory pism edytowane w portalu oraz wspólny nagłówek i stopka.</summary>
[ApiController]
[Route("api/document-templates")]
[Authorize]
public class DocumentTemplatesController : ControllerBase
{
    private readonly ILetterTemplateService _templates;

    public DocumentTemplatesController(ILetterTemplateService templates) => _templates = templates;

    private string UserId => User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value;
    private string? UserEmail => User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

    /// <summary>Wzory do wyboru przy generowaniu (aktywne); z <paramref name="all"/> – także wyłączone (do edycji).</summary>
    [HttpGet]
    [HasAnyPermission(Permissions.DocumentsGenerate, Permissions.DocumentTemplatesManage)]
    public async Task<ActionResult<IReadOnlyList<LetterTemplateDto>>> List([FromQuery] bool all, CancellationToken ct) =>
        Ok(await _templates.ListAsync(all, ct));

    [HttpGet("fields")]
    [HasAnyPermission(Permissions.DocumentsGenerate, Permissions.DocumentTemplatesManage)]
    public ActionResult<IReadOnlyList<LetterFieldDto>> Fields() => Ok(_templates.Fields());

    [HttpGet("{id:guid}")]
    [HasAnyPermission(Permissions.DocumentsGenerate, Permissions.DocumentTemplatesManage)]
    public async Task<ActionResult<LetterTemplateDto>> Get(Guid id, CancellationToken ct) =>
        await _templates.GetAsync(id, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpPost]
    [Audit("CreateLetterTemplate")]
    [HasPermission(Permissions.DocumentTemplatesManage)]
    public async Task<ActionResult<LetterTemplateDto>> Create(SaveLetterTemplateRequest request, CancellationToken ct) =>
        Ok(await _templates.CreateAsync(request, UserEmail, ct));

    [HttpPut("{id:guid}")]
    [Audit("UpdateLetterTemplate")]
    [HasPermission(Permissions.DocumentTemplatesManage)]
    public async Task<ActionResult<LetterTemplateDto>> Update(Guid id, SaveLetterTemplateRequest request, CancellationToken ct) =>
        await _templates.UpdateAsync(id, request, UserEmail, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpDelete("{id:guid}")]
    [Audit("DeleteLetterTemplate")]
    [HasPermission(Permissions.DocumentTemplatesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _templates.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Podgląd treści (także niezapisanej) jako PDF albo Word.</summary>
    [HttpPost("preview")]
    [HasAnyPermission(Permissions.DocumentsGenerate, Permissions.DocumentTemplatesManage)]
    public async Task<IActionResult> Preview(PreviewLetterRequest request, CancellationToken ct) =>
        this.LetterFileResult(await _templates.PreviewAsync(request, UserId, ct));

    [HttpGet("letterhead")]
    [HasAnyPermission(Permissions.DocumentsGenerate, Permissions.DocumentTemplatesManage)]
    public async Task<ActionResult<LetterheadDto>> GetLetterhead(CancellationToken ct) => Ok(await _templates.GetLetterheadAsync(ct));

    [HttpPut("letterhead")]
    [Audit("UpdateLetterhead")]
    [HasPermission(Permissions.DocumentTemplatesManage)]
    public async Task<ActionResult<LetterheadDto>> SaveLetterhead(SaveLetterheadRequest request, CancellationToken ct) =>
        Ok(await _templates.SaveLetterheadAsync(request, ct));

    [HttpGet("letterhead/logo")]
    [HasAnyPermission(Permissions.DocumentsGenerate, Permissions.DocumentTemplatesManage)]
    public async Task<IActionResult> GetLogo(CancellationToken ct) =>
        await _templates.GetLogoAsync(ct) is { } logo ? File(logo.Bytes, logo.ContentType) : NotFound();

    [HttpPut("letterhead/logo")]
    [Audit("UploadLetterheadLogo")]
    [HasPermission(Permissions.DocumentTemplatesManage)]
    [RequestSizeLimit(LetterTemplateService.MaxLogoBytes + 1024 * 1024)]
    public async Task<ActionResult<LetterheadDto>> UploadLogo(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await _templates.SaveLogoAsync(stream, file.ContentType, file.Length, ct));
    }

    [HttpDelete("letterhead/logo")]
    [Audit("DeleteLetterheadLogo")]
    [HasPermission(Permissions.DocumentTemplatesManage)]
    public async Task<ActionResult<LetterheadDto>> DeleteLogo(CancellationToken ct) => Ok(await _templates.DeleteLogoAsync(ct));
}
