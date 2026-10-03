namespace DokPortal.Application.Users;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserDto?> SetPersonAsync(string userId, Guid? personId, CancellationToken ct);
    Task<UserDto?> AssignRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct);
    Task<bool> ResetPasswordAsync(string userId, string newPassword, CancellationToken ct);
}
