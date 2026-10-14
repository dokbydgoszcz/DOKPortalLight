namespace DokPortal.Application.Users;

public class UserDto
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public Guid? PersonId { get; init; }
    public string? PersonFullName { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
    public bool IsActive { get; init; } = true;
    /// <summary>Konto ma włączoną weryfikację dwuetapową (aplikacja uwierzytelniająca).</summary>
    public bool MfaEnabled { get; init; }
}
