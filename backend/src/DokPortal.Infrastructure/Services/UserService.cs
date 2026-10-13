using DokPortal.Application.People;
using DokPortal.Application.Users;
using DokPortal.Domain.Constants;
using DokPortal.Infrastructure.Auth;
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
    private readonly SessionValidator? _sessions;

    public UserService(UserManager<AppUser> userManager, IPersonService personService, AppDbContext db, SessionValidator? sessions = null)
    {
        _userManager = userManager;
        _personService = personService;
        _db = db;
        _sessions = sessions;
    }

    private static void EnsureMayGrant(IEnumerable<string> roles, UserActor? actor)
    {
        if (actor is { IsAdministrator: false } && roles.Contains(AppRoles.Administrator))
        {
            throw new InvalidOperationException("Rolę Administrator może nadać tylko administrator.");
        }
    }

    /// <summary>Czy poza wskazanym kontem zostaje jakiś aktywny administrator.</summary>
    private async Task<bool> OtherActiveAdministratorExistsAsync(string exceptUserId)
    {
        var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Administrator);
        return admins.Any(u => u.Id != exceptUserId && !u.IsDeactivated);
    }

    /// <summary>Nowy znacznik bezpieczeństwa unieważnia wydane tokeny; czyścimy też pamięć sprawdzania sesji.</summary>
    private async Task EndSessionsAsync(AppUser user)
    {
        await _userManager.UpdateSecurityStampAsync(user);
        _sessions?.Invalidate(user.Id);
    }

    public async Task<UserDto?> SetActiveAsync(string userId, bool isActive, UserActor actor, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;
        if (!isActive)
        {
            if (user.Id == actor.UserId) throw new InvalidOperationException("Nie możesz wyłączyć własnego konta.");
            if (await _userManager.IsInRoleAsync(user, AppRoles.Administrator) && !await OtherActiveAdministratorExistsAsync(user.Id))
                throw new InvalidOperationException("To ostatni aktywny administrator – jego konta nie można wyłączyć.");
        }

        user.IsDeactivated = !isActive;
        await _userManager.UpdateAsync(user);
        await EndSessionsAsync(user);
        return ToDto(user, await _userManager.GetRolesAsync(user), await PersonNameAsync(user.PersonId, ct));
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

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct, UserActor? actor = null)
    {
        EnsureMayGrant(request.Roles, actor);
        if (request.PersonId is { } personId)
        {
            await EnsurePersonCanBeLinkedAsync(personId, null, ct);
        }
        var email = await ResolveLoginAsync(request, ct);

        var user = new AppUser { UserName = email, Email = email, PersonId = request.PersonId };

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

    /// <summary>
    /// Login to e-mail osoby (nie podaje się go drugi raz). Osoba bez e-maila dostaje wpisany e-mail także jako swój adres.
    /// Konto bez osoby używa wpisanego e-maila.
    /// </summary>
    private async Task<string> ResolveLoginAsync(CreateUserRequest request, CancellationToken ct)
    {
        var typed = request.Email?.Trim();
        if (request.PersonId is not { } personId)
        {
            return string.IsNullOrEmpty(typed) ? throw new InvalidOperationException("Podaj e-mail (login).") : typed;
        }

        var person = await _db.People.FirstAsync(p => p.Id == personId, ct);
        if (!string.IsNullOrWhiteSpace(person.Email)) return person.Email.Trim();
        if (string.IsNullOrEmpty(typed))
            throw new InvalidOperationException("Ta osoba nie ma e-maila – podaj go (będzie loginem i zapiszemy go też przy osobie).");

        var normalized = typed.ToLowerInvariant();
        var holder = await _db.People.AsNoTracking()
            .Where(p => p.Id != personId && p.Email != null && p.Email.ToLower() == normalized)
            .Select(p => p.FirstName + " " + p.LastName).FirstOrDefaultAsync(ct);
        if (holder is not null) throw new InvalidOperationException($"Ten adres e-mail ma już: {holder}.");
        person.Email = typed;
        person.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return typed;
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

    public async Task<UserDto?> AssignRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct, UserActor? actor = null)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(AppRoles.Administrator)) EnsureMayGrant(roles, actor);
        if (actor is { IsAdministrator: false } && currentRoles.Contains(AppRoles.Administrator))
            throw new InvalidOperationException("Role administratora może zmienić tylko administrator.");
        if (currentRoles.Contains(AppRoles.Administrator) && !roles.Contains(AppRoles.Administrator)
            && !user.IsDeactivated && !await OtherActiveAdministratorExistsAsync(user.Id))
            throw new InvalidOperationException("To ostatni aktywny administrator – nie można odebrać mu tej roli.");

        if (currentRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }
        if (roles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, roles);
        }
        await EndSessionsAsync(user);

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

        // Nowe hasło zmienia znacznik bezpieczeństwa (Identity) – stare sesje wygasają.
        _sessions?.Invalidate(user.Id);
        return true;
    }

    private static UserDto ToDto(AppUser user, IList<string> roles, string? personFullName) => new()
    {
        Id = user.Id,
        Email = user.Email!,
        PersonId = user.PersonId,
        PersonFullName = personFullName,
        Roles = roles.ToList(),
        IsActive = !user.IsDeactivated
    };
}
