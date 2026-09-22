using System.IdentityModel.Tokens.Jwt;
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

    public DocumentsController(IDocumentService documentService) => _documentService = documentService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GeneratedDocumentDto>>> GetHistory(CancellationToken ct)
        => Ok(await _documentService.GetHistoryAsync(ct));

    [HttpPost("generate")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP},{AppRoles.DyrektorDOK}")]
    public async Task<IActionResult> Generate(GenerateDocumentRequest request, CancellationToken ct)
    {
        var userId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var result = await _documentService.GenerateAsync(request, userId, ct);
        if (result is null) return NotFound();
        return File(result.PdfBytes, "application/pdf", $"{result.History.Template}.pdf");
    }
}
