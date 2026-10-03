using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Documents;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IAuditLogService _auditLogService;

    public DocumentsController(IDocumentService documentService, IAuditLogService auditLogService)
    {
        _documentService = documentService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    [HasPermission(Permissions.DocumentsView)]
    public async Task<ActionResult<IReadOnlyList<GeneratedDocumentDto>>> GetHistory(CancellationToken ct)
        => Ok(await _documentService.GetHistoryAsync(ct));

    [HttpPost("generate")]
    [HasPermission(Permissions.DocumentsGenerate)]
    public async Task<IActionResult> Generate(GenerateDocumentRequest request, CancellationToken ct)
    {
        var userId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var result = await _documentService.GenerateAsync(request, userId, ct);
        if (result is null) return NotFound();
        return File(result.PdfBytes, "application/pdf", $"{result.History.Template}.pdf");
    }

    [HttpGet("{id:guid}/download")]
    [HasPermission(Permissions.DocumentsView)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var download = await _documentService.DownloadAsync(id, ct);
        return download is null ? NotFound() : File(download.PdfBytes, "application/pdf", download.FileName);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.DocumentsGenerate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var removed = await _documentService.DeleteAsync(id, ct);
        if (removed is null) return NotFound();

        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var currentUserEmail = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value;
        await _auditLogService.LogAsync(
            currentUserId, currentUserEmail, "DeleteGeneratedDocument", $"{removed.Template} – {removed.PersonFullName}",
            DokPortal.Domain.Enums.AuditResult.Allowed, ct);
        return NoContent();
    }
}
