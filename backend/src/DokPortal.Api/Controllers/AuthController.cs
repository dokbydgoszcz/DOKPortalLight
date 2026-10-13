using DokPortal.Application.Auth;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Permissions;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Auth;
using DokPortal.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    private readonly IAccountService _accountService;

    public AuthController(
        UserManager<AppUser> userManager,
        IJwtTokenGenerator tokenGenerator,
        JwtOptions jwtOptions,
        IAuditLogService auditLogService,
        IPermissionService permissionService,
        IAccountService accountService)
    {
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
        _jwtOptions = jwtOptions;
        _auditLogService = auditLogService;
        _permissionService = permissionService;
        _accountService = accountService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
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

        if (user.IsDeactivated)
        {
            await _auditLogService.LogAsync(user.Id, request.Email, "Login", "Konto nieaktywne", AuditResult.Blocked, ct);
            return Unauthorized(new { message = "Konto jest nieaktywne. Skontaktuj się z administratorem." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        if (string.IsNullOrEmpty(user.SecurityStamp))
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }

        await _auditLogService.LogAsync(user.Id, user.Email!, "Login", "Zalogowano", AuditResult.Allowed, ct);
        return Ok(await SessionForAsync(user, ct));
    }

    private async Task<LoginResponse> SessionForAsync(AppUser user, CancellationToken ct)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await _permissionService.GetPermissionsForRolesAsync(roles, ct);
        var token = _tokenGenerator.GenerateToken(user.Id, user.Email!, user.PersonId, roles, permissions, user.SecurityStamp);
        return new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
            Roles = roles.ToList(),
            PersonId = user.PersonId,
            MustChangePassword = user.MustChangePassword
        };
    }

    /// <summary>Zmiana własnego hasła; zwraca nową sesję (stare tokeny wygasają).</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!.Value;
        var email = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value ?? "";
        try
        {
            await _accountService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
        }
        catch (InvalidOperationException ex)
        {
            await _auditLogService.LogAsync(userId, email, "ChangePassword", ex.Message, AuditResult.Blocked, ct);
            throw;
        }
        await _auditLogService.LogAsync(userId, email, "ChangePassword", "Zmieniono własne hasło", AuditResult.Allowed, ct);
        var user = await _userManager.FindByIdAsync(userId);
        return Ok(await SessionForAsync(user!, ct));
    }

    public const string ForgotPasswordAnswer =
        "Jeśli w portalu jest aktywne konto z tym adresem, wysłaliśmy na nie link do ustawienia nowego hasła. Sprawdź skrzynkę (także spam).";

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await _accountService.RequestPasswordResetAsync(request.Email, ct);
        await _auditLogService.LogAsync(string.Empty, request.Email, "ForgotPassword", "Prośba o link do zmiany hasła", AuditResult.Allowed, ct);
        return Ok(new { message = ForgotPasswordAnswer });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword(ResetPasswordWithTokenRequest request, CancellationToken ct)
    {
        try
        {
            await _accountService.ResetPasswordWithTokenAsync(request.UserId, request.Token, request.NewPassword, ct);
        }
        catch (InvalidOperationException ex)
        {
            await _auditLogService.LogAsync(request.UserId, string.Empty, "ResetPasswordWithLink", ex.Message, AuditResult.Blocked, ct);
            throw;
        }
        await _auditLogService.LogAsync(request.UserId, string.Empty, "ResetPasswordWithLink", "Ustawiono nowe hasło z linku", AuditResult.Allowed, ct);
        return NoContent();
    }
}
