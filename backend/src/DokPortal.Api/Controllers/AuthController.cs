using DokPortal.Application.Auth;
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

    public AuthController(UserManager<AppUser> userManager, IJwtTokenGenerator tokenGenerator, JwtOptions jwtOptions)
    {
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
        _jwtOptions = jwtOptions;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Nieprawidłowy e-mail lub hasło." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenGenerator.GenerateToken(user.Id, user.Email!, user.PersonId, roles);

        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
            Roles = roles.ToList(),
            PersonId = user.PersonId
        });
    }
}
