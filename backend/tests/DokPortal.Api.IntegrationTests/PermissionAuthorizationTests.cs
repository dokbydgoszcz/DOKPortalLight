using System.Net;
using System.Net.Http.Json;
using DokPortal.Api.Authorization;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class PermissionAuthorizationTests : IntegrationTestBase
{
    public PermissionAuthorizationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task PolicyProvider_CreatesPolicyForKnownPermission_AndDelegatesOthers()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var known = await provider.GetPolicyAsync(Permissions.PeopleManage);
        var unknown = await provider.GetPolicyAsync("NieistniejacaPolityka");

        Assert.NotNull(known);
        Assert.Contains(known!.Requirements, r => r is PermissionRequirement p && p.Permission == Permissions.PeopleManage);
        Assert.Null(unknown);
    }

    [Theory]
    [InlineData("/api/users", "Administrator", HttpStatusCode.OK)]
    [InlineData("/api/users", "DyrektorDOK", HttpStatusCode.Forbidden)]
    [InlineData("/api/audit-log", "Administrator", HttpStatusCode.OK)]
    [InlineData("/api/audit-log", "DyrektorSKSP", HttpStatusCode.Forbidden)]
    public async Task AdminOnlyEndpoints_AreGatedByPermission(string url, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_ReturnsUnauthorized_WithoutToken()
    {
        var response = await Client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("DyrektorSKSP", HttpStatusCode.Created)]
    [InlineData("DyrektorDOK", HttpStatusCode.Created)]
    [InlineData("Superwizor", HttpStatusCode.Forbidden)]
    [InlineData("Biskup", HttpStatusCode.Forbidden)]
    public async Task People_Create_RequiresManagePermission(string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.PostAsJsonAsync("/api/people", new { FirstName = "Test", LastName = "User" });

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("KatechistaProwadzacy")]
    [InlineData("Biskup")]
    public async Task People_AndParishes_Lists_AreOpenToEveryAuthenticatedUser(string role)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/people")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/parishes")).StatusCode);
    }
}
