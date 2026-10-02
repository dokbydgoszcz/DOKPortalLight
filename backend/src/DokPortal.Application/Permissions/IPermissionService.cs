namespace DokPortal.Application.Permissions;

public interface IPermissionService
{
    Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(IEnumerable<string> roles, CancellationToken ct);
    Task<PermissionMatrixDto> GetMatrixAsync(CancellationToken ct);
    Task UpdateRolePermissionsAsync(string role, IReadOnlyCollection<string> permissions, CancellationToken ct);
}
