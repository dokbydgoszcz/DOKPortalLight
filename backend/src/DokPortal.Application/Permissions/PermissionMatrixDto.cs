using DokPortal.Domain.Constants;

namespace DokPortal.Application.Permissions;

public class PermissionMatrixDto
{
    public required IReadOnlyList<string> Roles { get; init; }
    public required IReadOnlyList<PermissionInfo> Permissions { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Grants { get; init; }
}
