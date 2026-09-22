using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class UsersControllerTests : IntegrationTestBase
{
    public UsersControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenAssignRoles_UpdatesUserRoles()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var newUserEmail = $"nowy-{Guid.NewGuid():N}@example.org";

        var createResponse = await admin.PostAsJsonAsync("/api/users", new
        {
            Email = newUserEmail,
            Password = "Sekret123!",
            Roles = new[] { "KatechistaProwadzacy" }
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        var assignResponse = await admin.PutAsJsonAsync($"/api/users/{created!.Id}/roles", new { Roles = new[] { "Superwizor" } });
        assignResponse.EnsureSuccessStatusCode();
        var updated = await assignResponse.Content.ReadFromJsonAsync<UserDto>();

        Assert.Contains("Superwizor", updated!.Roles);
        Assert.DoesNotContain("KatechistaProwadzacy", updated.Roles);
    }

    [Fact]
    public async Task List_WithoutAdministratorRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
