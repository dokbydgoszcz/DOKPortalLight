using DokPortal.Api.Authorization;
using DokPortal.Application.AuditLog;
using DokPortal.Domain.Constants;
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
    public async Task<ActionResult<IReadOnlyList<AuditLogEntryDto>>> List(CancellationToken ct)
        => Ok(await _auditLogService.ListAsync(ct));
}
