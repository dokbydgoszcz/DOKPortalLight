namespace DokPortal.Application.Users;

public class AssignRolesRequest
{
    public required IReadOnlyList<string> Roles { get; init; }
}
