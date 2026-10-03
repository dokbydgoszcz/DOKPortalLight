using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Permissions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/permissions")]
[HasPermission(Permissions.PermissionsManage)]
public class PermissionsController : ControllerBase
{
    private const int AuditDescriptionMaxLength = 300;

    private readonly IPermissionService _permissionService;
    private readonly IAuditLogService _auditLogService;

    public PermissionsController(IPermissionService permissionService, IAuditLogService auditLogService)
    {
        _permissionService = permissionService;
        _auditLogService = auditLogService;
    }

    [HttpGet("matrix")]
    public async Task<ActionResult<PermissionMatrixDto>> GetMatrix(CancellationToken ct)
        => Ok(await _permissionService.GetMatrixAsync(ct));

    [HttpPut("roles/{role}")]
    public async Task<IActionResult> UpdateRole(string role, UpdateRolePermissionsRequest request, CancellationToken ct)
    {
        var matrix = await _permissionService.GetMatrixAsync(ct);
        var before = matrix.Grants.TryGetValue(role, out var current) ? current : Array.Empty<string>();

        await _permissionService.UpdateRolePermissionsAsync(role, request.Permissions, ct);

        var added = request.Permissions.Except(before).OrderBy(p => p);
        var removed = before.Except(request.Permissions).OrderBy(p => p);
        await AuditAsync("UpdateRolePermissions",
            $"{role}: dodano [{string.Join(", ", added)}]; odebrano [{string.Join(", ", removed)}]", ct);
        return NoContent();
    }

    [HttpPost("roles")]
    public async Task<ActionResult<RoleInfoDto>> CreateRole(CreateRoleRequest request, CancellationToken ct)
    {
        var created = await _permissionService.CreateRoleAsync(request.Name, ct);
        await AuditAsync("CreateRole", created.Name, ct);
        return Created($"/api/permissions/roles/{Uri.EscapeDataString(created.Name)}", created);
    }

    [HttpDelete("roles/{role}")]
    public async Task<IActionResult> DeleteRole(string role, CancellationToken ct)
    {
        await _permissionService.DeleteRoleAsync(role, ct);
        await AuditAsync("DeleteRole", role, ct);
        return NoContent();
    }

    private Task AuditAsync(string action, string description, CancellationToken ct)
    {
        var truncated = description.Length > AuditDescriptionMaxLength ? description[..AuditDescriptionMaxLength] : description;
        return _auditLogService.LogAsync(
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value,
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value,
            action, truncated, AuditResult.Allowed, ct);
    }
}
