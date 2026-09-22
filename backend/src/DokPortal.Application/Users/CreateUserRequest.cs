namespace DokPortal.Application.Users;

public class CreateUserRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public Guid? PersonId { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}
