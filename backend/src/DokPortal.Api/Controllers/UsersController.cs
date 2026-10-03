using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Permissions;
using DokPortal.Application.Users;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/users")]
[HasPermission(Permissions.UsersManage)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuditLogService _auditLogService;
    private readonly IPermissionService _permissionService;

    public UsersController(IUserService userService, IAuditLogService auditLogService, IPermissionService permissionService)
    {
        _userService = userService;
        _auditLogService = auditLogService;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(CancellationToken ct)
        => Ok(await _userService.ListAsync(ct));

    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<string>>> ListRoles(CancellationToken ct)
        => Ok(await _permissionService.ListRoleNamesAsync(ct));

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var created = await _userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(List), created);
    }

    [HttpPut("{id}/roles")]
    public async Task<ActionResult<UserDto>> AssignRoles(string id, AssignRolesRequest request, CancellationToken ct)
    {
        var updated = await _userService.AssignRolesAsync(id, request.Roles, ct);
        if (updated is null) return NotFound();

        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var currentUserEmail = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value;
        await _auditLogService.LogAsync(currentUserId, currentUserEmail, "AssignUserRoles", updated.Email, DokPortal.Domain.Enums.AuditResult.Allowed, ct);

        return Ok(updated);
    }

    [HttpPut("{id}/person")]
    public async Task<ActionResult<UserDto>> SetPerson(string id, SetUserPersonRequest request, CancellationToken ct)
    {
        var updated = await _userService.SetPersonAsync(id, request.PersonId, ct);
        if (updated is null) return NotFound();

        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var currentUserEmail = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value;
        await _auditLogService.LogAsync(currentUserId, currentUserEmail, "SetUserPerson", updated.Email, DokPortal.Domain.Enums.AuditResult.Allowed, ct);

        return Ok(updated);
    }

    [HttpPut("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, ResetPasswordRequest request, CancellationToken ct)
    {
        var succeeded = await _userService.ResetPasswordAsync(id, request.NewPassword, ct);
        if (!succeeded) return NotFound();

        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var currentUserEmail = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value;
        await _auditLogService.LogAsync(currentUserId, currentUserEmail, "ResetUserPassword", id, DokPortal.Domain.Enums.AuditResult.Allowed, ct);

        return NoContent();
    }
}
