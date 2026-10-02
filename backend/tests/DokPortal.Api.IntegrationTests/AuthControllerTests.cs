using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Auth;
using DokPortal.Domain.Constants;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndRoles()
    {
        var email = $"user-{Guid.NewGuid():N}@example.org";
        const string password = "Sekret123!";
        await CreateUserAndGetTokenAsync(email, password, "Administrator");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Contains("Administrator", body.Roles);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var email = $"user-{Guid.NewGuid():N}@example.org";
        await CreateUserAndGetTokenAsync(email, "Sekret123!", "Administrator");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_TokenContainsPermissionClaimsMatchingTheRole()
    {
        var email = $"dyr-{Guid.NewGuid():N}@example.org";
        await CreateUserAndGetTokenAsync(email, "Sekret123!", "DyrektorDOK");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Sekret123!" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        var permissions = new JwtSecurityTokenHandler().ReadJwtToken(body!.Token).Claims
            .Where(c => c.Type == "permission").Select(c => c.Value).ToList();
        Assert.Contains(Permissions.DokCasesManage, permissions);
        Assert.DoesNotContain(Permissions.CandidatesManage, permissions);
    }

    [Fact]
    public async Task Login_AdministratorTokenContainsEntireCatalog()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.org";
        await CreateUserAndGetTokenAsync(email, "Sekret123!", "Administrator");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Sekret123!" });
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        var permissions = new JwtSecurityTokenHandler().ReadJwtToken(body!.Token).Claims
            .Where(c => c.Type == "permission").ToList();
        Assert.Equal(PermissionCatalog.All.Count, permissions.Count);
    }
}
