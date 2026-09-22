namespace DokPortal.Application.Auth;

public interface IJwtTokenGenerator
{
    string GenerateToken(string userId, string email, Guid? personId, IEnumerable<string> roles);
}
