namespace DokPortal.Application.Auth;

public class LoginResponse
{
    public required string Token { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
    public Guid? PersonId { get; init; }
}
