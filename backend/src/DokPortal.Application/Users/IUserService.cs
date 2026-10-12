namespace DokPortal.Application.Users;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct);
    /// <exception cref="InvalidOperationException">Rolę Administrator nadaje tylko administrator.</exception>
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct, UserActor? actor = null);
    Task<UserDto?> SetPersonAsync(string userId, Guid? personId, CancellationToken ct);
    /// <summary>Zastępuje role konta i kończy jego otwarte sesje (nowe role działają od razu).</summary>
    /// <exception cref="InvalidOperationException">Nadanie roli Administrator przez nie-administratora albo odebranie jej ostatniemu aktywnemu administratorowi.</exception>
    Task<UserDto?> AssignRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct, UserActor? actor = null);
    /// <summary>Wyłącza albo włącza konto; wyłączenie kończy otwarte sesje.</summary>
    /// <exception cref="InvalidOperationException">Własne konto albo ostatni aktywny administrator.</exception>
    Task<UserDto?> SetActiveAsync(string userId, bool isActive, UserActor actor, CancellationToken ct);
    Task<bool> ResetPasswordAsync(string userId, string newPassword, CancellationToken ct);
}
