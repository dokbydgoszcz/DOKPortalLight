using DokPortal.Api.Authorization;
using DokPortal.Application.AuditLog;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/audit-log")]
[HasPermission(Permissions.AuditLogView)]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogController(IAuditLogService auditLogService) => _auditLogService = auditLogService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogEntryDto>>> List(
        [FromQuery] string? search, [FromQuery] string? action, [FromQuery] AuditResult? result,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int take = AuditLogFilter.DefaultTake,
        CancellationToken ct = default)
        => Ok(await _auditLogService.ListAsync(
            new AuditLogFilter { Search = search, Action = action, Result = result, From = from, To = to, Take = take }, ct));

    [HttpGet("actions")]
    public async Task<ActionResult<IReadOnlyList<string>>> ListActions(CancellationToken ct)
        => Ok(await _auditLogService.ListActionsAsync(ct));
}
