using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Users;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = AppRoles.Administrator)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuditLogService _auditLogService;

    public UsersController(IUserService userService, IAuditLogService auditLogService)
    {
        _userService = userService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(CancellationToken ct)
        => Ok(await _userService.ListAsync(ct));

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
}
