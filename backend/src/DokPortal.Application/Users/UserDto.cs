namespace DokPortal.Application.Users;

public class UserDto
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public Guid? PersonId { get; init; }
    public string? PersonFullName { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}
