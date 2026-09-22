using System.IdentityModel.Tokens.Jwt;
using DokPortal.Infrastructure.Auth;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Auth;

public class JwtTokenGeneratorTests
{
    [Fact]
    public void GenerateToken_IncludesRoleAndPersonIdClaims()
    {
        var options = new JwtOptions
        {
            Key = "unit-test-signing-key-1234567890123456",
            Issuer = "test",
            Audience = "test",
            ExpiryMinutes = 60
        };
        var generator = new JwtTokenGenerator(options);
        var personId = Guid.NewGuid();

        var token = generator.GenerateToken("user-1", "a@b.pl", personId, new[] { "Administrator" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Administrator");
        Assert.Contains(jwt.Claims, c => c.Type == "personId" && c.Value == personId.ToString());
    }
}
