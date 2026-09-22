using DokPortal.Application.Users;
using DokPortal.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<AppUser> _userManager;

    public UserService(UserManager<AppUser> userManager) => _userManager = userManager;

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct)
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync(ct);
        var result = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            result.Add(ToDto(user, await _userManager.GetRolesAsync(user)));
        }
        return result;
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var user = new AppUser { UserName = request.Email, Email = request.Email, PersonId = request.PersonId };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Nie udało się utworzyć użytkownika: {errors}");
        }

        if (request.Roles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, request.Roles);
        }

        return ToDto(user, await _userManager.GetRolesAsync(user));
    }

    public async Task<UserDto?> AssignRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }
        if (roles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, roles);
        }

        return ToDto(user, await _userManager.GetRolesAsync(user));
    }

    private static UserDto ToDto(AppUser user, IList<string> roles) => new()
    {
        Id = user.Id,
        Email = user.Email!,
        PersonId = user.PersonId,
        Roles = roles.ToList()
    };
}
