using DokPortal.Application.Auth;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Permissions;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Auth;
using DokPortal.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly JwtOptions _jwtOptions;
    private readonly IAuditLogService _auditLogService;
    private readonly IPermissionService _permissionService;

    public AuthController(
        UserManager<AppUser> userManager,
        IJwtTokenGenerator tokenGenerator,
        JwtOptions jwtOptions,
        IAuditLogService auditLogService,
        IPermissionService permissionService)
    {
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
        _jwtOptions = jwtOptions;
        _auditLogService = auditLogService;
        _permissionService = permissionService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is not null && await _userManager.IsLockedOutAsync(user))
        {
            await _auditLogService.LogAsync(user.Id, request.Email, "Login", "Konto zablokowane", AuditResult.Blocked, ct);
            return Unauthorized(new { message = "Konto zostało tymczasowo zablokowane z powodu wielu nieudanych prób logowania. Spróbuj ponownie za kilkanaście minut." });
        }

        var passwordValid = user is not null && await _userManager.CheckPasswordAsync(user, request.Password);
        if (user is null || !passwordValid)
        {
            if (user is not null)
            {
                await _userManager.AccessFailedAsync(user);
            }
            await _auditLogService.LogAsync(user?.Id ?? string.Empty, request.Email, "Login", "Nieprawidłowe dane logowania", AuditResult.Blocked, ct);
            return Unauthorized(new { message = "Nieprawidłowy e-mail lub hasło." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await _permissionService.GetPermissionsForRolesAsync(roles, ct);
        var token = _tokenGenerator.GenerateToken(user.Id, user.Email!, user.PersonId, roles, permissions);

        await _auditLogService.LogAsync(user.Id, user.Email!, "Login", "Zalogowano", AuditResult.Allowed, ct);

        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
            Roles = roles.ToList(),
            PersonId = user.PersonId
        });
    }
}
