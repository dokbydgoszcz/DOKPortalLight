using DokPortal.Application.People;
using DokPortal.Application.Users;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IPersonService _personService;
    private readonly AppDbContext _db;

    public UserService(UserManager<AppUser> userManager, IPersonService personService, AppDbContext db)
    {
        _userManager = userManager;
        _personService = personService;
        _db = db;
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct)
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync(ct);
        var personIds = users.Where(u => u.PersonId != null).Select(u => u.PersonId!.Value).Distinct().ToList();
        var names = await _db.People.AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FullName, ct);

        var result = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            var personName = user.PersonId is { } id && names.TryGetValue(id, out var name) ? name : null;
            result.Add(ToDto(user, await _userManager.GetRolesAsync(user), personName));
        }
        return result;
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        if (request.PersonId is { } personId)
        {
            await EnsurePersonCanBeLinkedAsync(personId, null, ct);
        }

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

        return ToDto(user, await _userManager.GetRolesAsync(user), await PersonNameAsync(user.PersonId, ct));
    }

    public async Task<UserDto?> SetPersonAsync(string userId, Guid? personId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        if (personId is { } id)
        {
            await EnsurePersonCanBeLinkedAsync(id, user.Id, ct);
        }

        user.PersonId = personId;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Nie udało się powiązać konta z osobą: {errors}");
        }

        return ToDto(user, await _userManager.GetRolesAsync(user), await PersonNameAsync(personId, ct));
    }

    /// <summary>Osoba musi istnieć i nie może mieć już innego konta (e-mail osoby bywa loginem – jedna osoba, jedno konto).</summary>
    private async Task EnsurePersonCanBeLinkedAsync(Guid personId, string? exceptUserId, CancellationToken ct)
    {
        if (await _personService.GetByIdAsync(personId, ct) is null)
        {
            throw new InvalidOperationException("Wskazana osoba nie istnieje.");
        }

        var otherAccount = await _userManager.Users.AsNoTracking()
            .Where(u => u.PersonId == personId && u.Id != exceptUserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);
        if (otherAccount is not null)
        {
            throw new InvalidOperationException($"Ta osoba ma już konto: {otherAccount}.");
        }
    }

    private async Task<string?> PersonNameAsync(Guid? personId, CancellationToken ct) =>
        personId is { } id ? (await _personService.GetByIdAsync(id, ct))?.FullName : null;

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

        return ToDto(user, await _userManager.GetRolesAsync(user), await PersonNameAsync(user.PersonId, ct));
    }

    public async Task<bool> ResetPasswordAsync(string userId, string newPassword, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return false;

        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            var errors = string.Join("; ", removeResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Nie udało się zresetować hasła: {errors}");
        }

        var result = await _userManager.AddPasswordAsync(user, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Nie udało się zresetować hasła: {errors}");
        }

        return true;
    }

    private static UserDto ToDto(AppUser user, IList<string> roles, string? personFullName) => new()
    {
        Id = user.Id,
        Email = user.Email!,
        PersonId = user.PersonId,
        PersonFullName = personFullName,
        Roles = roles.ToList()
    };
}
