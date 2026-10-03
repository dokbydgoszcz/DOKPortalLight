namespace DokPortal.Application.Permissions;

public class UpdateRolePermissionsRequest
{
    public required IReadOnlyList<string> Permissions { get; init; }
}
