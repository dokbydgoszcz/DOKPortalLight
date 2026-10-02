using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Export;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IExportService _exportService;
    private readonly IAuditLogService _auditLogService;

    public ExportController(IExportService exportService, IAuditLogService auditLogService)
    {
        _exportService = exportService;
        _auditLogService = auditLogService;
    }

    [HttpGet("people")]
    [HasPermission(Permissions.PeopleExport)]
    public Task<IActionResult> People(CancellationToken ct) => ExportAsync("people", "osoby", _exportService.ExportPeopleAsync, ct);

    [HttpGet("dok-cases")]
    [HasPermission(Permissions.DokCasesExport)]
    public Task<IActionResult> DokCases(CancellationToken ct) => ExportAsync("dok-cases", "podopieczni-dok", _exportService.ExportDokCasesAsync, ct);

    [HttpGet("candidates")]
    [HasPermission(Permissions.CandidatesExport)]
    public Task<IActionResult> Candidates(CancellationToken ct) => ExportAsync("candidates", "kandydaci-sksp", _exportService.ExportCandidatesAsync, ct);

    [HttpGet("missions")]
    [HasPermission(Permissions.MissionsExport)]
    public Task<IActionResult> Missions(CancellationToken ct) => ExportAsync("missions", "katechisci-poslani", _exportService.ExportMissionsAsync, ct);

    [HttpGet("formators")]
    [HasPermission(Permissions.FormatorsExport)]
    public Task<IActionResult> Formators(CancellationToken ct) => ExportAsync("formators", "formatorzy", _exportService.ExportFormatorsAsync, ct);

    [HttpGet("supervisions")]
    [HasPermission(Permissions.SupervisionsExport)]
    public Task<IActionResult> Supervisions(CancellationToken ct) => ExportAsync("supervisions", "superwizje", _exportService.ExportSupervisionsAsync, ct);

    [HttpGet("meetings")]
    [HasPermission(Permissions.MeetingsExport)]
    public Task<IActionResult> Meetings(CancellationToken ct) => ExportAsync("meetings", "spotkania", _exportService.ExportMeetingsAsync, ct);

    [HttpGet("parishes")]
    [HasPermission(Permissions.ParishesExport)]
    public Task<IActionResult> Parishes(CancellationToken ct) => ExportAsync("parishes", "parafie", _exportService.ExportParishesAsync, ct);

    private async Task<IActionResult> ExportAsync(
        string listName, string fileName, Func<CancellationToken, Task<byte[]>> export, CancellationToken ct)
    {
        var bytes = await export(ct);

        await _auditLogService.LogAsync(
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value,
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value,
            "ExportData", listName, AuditResult.Allowed, ct);

        return File(bytes, XlsxContentType, $"{fileName}-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
    }
}
