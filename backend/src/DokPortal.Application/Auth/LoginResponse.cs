namespace DokPortal.Application.Auth;

public class LoginResponse
{
    public required string Token { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
    public Guid? PersonId { get; init; }
    /// <summary>Konto ma hasło nadane przez administratora – trzeba je zmienić przed pracą.</summary>
    public bool MustChangePassword { get; init; }
}
