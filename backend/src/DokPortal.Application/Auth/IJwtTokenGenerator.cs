namespace DokPortal.Application.Auth;

public interface IJwtTokenGenerator
{
    /// <param name="securityStamp">Znacznik bezpieczeństwa konta (claim „stamp”); jego zmiana unieważnia token.</param>
    string GenerateToken(string userId, string email, Guid? personId, IEnumerable<string> roles, IEnumerable<string> permissions, string? securityStamp = null);
}
