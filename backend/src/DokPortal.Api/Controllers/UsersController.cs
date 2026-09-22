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

    public UsersController(IUserService userService) => _userService = userService;

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
        return updated is null ? NotFound() : Ok(updated);
    }
}
