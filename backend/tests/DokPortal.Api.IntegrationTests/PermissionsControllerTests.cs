using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Permissions;
using DokPortal.Domain.Constants;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class PermissionsControllerTests : IntegrationTestBase
{
    public PermissionsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private static string NewRoleName() => $"Rola {Guid.NewGuid():N}";

    private Task<HttpClient> AdminAsync() =>
        CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    private static string RoleUrl(string role) => $"/api/permissions/roles/{Uri.EscapeDataString(role)}";

    private bool HasAudit(string action, string startsWith)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.AuditLogEntries.Any(e => e.Action == action && e.ObjectDescription.StartsWith(startsWith));
    }

    [Fact]
    public async Task Matrix_ForAdministrator_ReturnsCatalogAndSystemRolesWithoutAdministrator()
    {
        var admin = await AdminAsync();

        var response = await admin.GetAsync("/api/permissions/matrix");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var matrix = await response.Content.ReadFromJsonAsync<PermissionMatrixDto>();
        Assert.Equal(PermissionCatalog.All.Count, matrix!.Permissions.Count);
        Assert.DoesNotContain(matrix.Roles, r => r.Name == "Administrator");
        Assert.Contains(matrix.Roles, r => r.Name == "Biskup" && r.IsSystem);
        Assert.Contains(Permissions.DokCasesManage, matrix.Grants["DyrektorDOK"]);
    }

    [Theory]
    [InlineData("DyrektorDOK")]
    [InlineData("DyrektorSKSP")]
    [InlineData("KatechistaProwadzacy")]
    public async Task AllEndpoints_AreForbidden_ForNonAdministrators(string role)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/permissions/matrix")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/permissions/roles", new { Name = "Test" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(RoleUrl("Biskup"), new { Permissions = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync(RoleUrl("Biskup"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users/roles")).StatusCode);
    }

    [Fact]
    public async Task Matrix_ReturnsUnauthorized_WithoutToken()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/permissions/matrix")).StatusCode);
    }

    [Fact]
    public async Task CreateRole_ReturnsCreated_ShowsInMatrixAndUsersRoles_AndIsAudited()
    {
        var admin = await AdminAsync();
        var name = NewRoleName();

        var response = await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = name });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<RoleInfoDto>();
        Assert.Equal(name, created!.Name);
        Assert.False(created.IsSystem);
        var matrix = await (await admin.GetAsync("/api/permissions/matrix")).Content.ReadFromJsonAsync<PermissionMatrixDto>();
        Assert.Contains(matrix!.Roles, r => r.Name == name && !r.IsSystem);
        Assert.Empty(matrix.Grants[name]);
        var roles = await (await admin.GetAsync("/api/users/roles")).Content.ReadFromJsonAsync<List<string>>();
        Assert.Contains(name, roles!);
        Assert.Contains("Administrator", roles!);
        Assert.True(HasAudit("CreateRole", name));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Rola!")]
    [InlineData("Biskup")]
    public async Task CreateRole_ReturnsBadRequest_ForInvalidOrDuplicateName(string name)
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_SavesGrants_ReturnsBadRequestForUnknownPermission_AndIsAudited()
    {
        var admin = await AdminAsync();
        var name = NewRoleName();
        await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = name });

        var ok = await admin.PutAsJsonAsync(RoleUrl(name), new { Permissions = new[] { Permissions.CandidatesView, Permissions.MeetingsView } });
        var bad = await admin.PutAsJsonAsync(RoleUrl(name), new { Permissions = new[] { "People.Fly" } });

        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var matrix = await (await admin.GetAsync("/api/permissions/matrix")).Content.ReadFromJsonAsync<PermissionMatrixDto>();
        Assert.Equal(new[] { Permissions.CandidatesView, Permissions.MeetingsView }, matrix!.Grants[name].OrderBy(p => p).ToArray());
        Assert.True(HasAudit("UpdateRolePermissions", name));
    }

    [Fact]
    public async Task UpdateRole_ReturnsBadRequest_ForAdministratorAndUnknownRole()
    {
        var admin = await AdminAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(RoleUrl("Administrator"), new { Permissions = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(RoleUrl("NieistniejacaRola"), new { Permissions = Array.Empty<string>() })).StatusCode);
    }

    [Fact]
    public async Task DeleteRole_RemovesCustomRole_RejectsSystemRoleAndRoleWithUsers_AndIsAudited()
    {
        var admin = await AdminAsync();
        var removable = NewRoleName();
        var inUse = NewRoleName();
        await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = removable });
        await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = inUse });
        await CreateUserAndGetTokenAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", inUse);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(RoleUrl(removable))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync(RoleUrl("Biskup"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync(RoleUrl(inUse))).StatusCode);

        var matrix = await (await admin.GetAsync("/api/permissions/matrix")).Content.ReadFromJsonAsync<PermissionMatrixDto>();
        Assert.DoesNotContain(matrix!.Roles, r => r.Name == removable);
        Assert.Contains(matrix.Roles, r => r.Name == inUse);
        Assert.True(HasAudit("DeleteRole", removable));
    }

    [Fact]
    public async Task CustomRole_GainsAccessOnNextRequest_AfterGrantingPermission()
    {
        var admin = await AdminAsync();
        var name = NewRoleName();
        await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = name });
        var user = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", name);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/candidates")).StatusCode);

        await admin.PutAsJsonAsync(RoleUrl(name), new { Permissions = new[] { Permissions.CandidatesView } });

        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/candidates")).StatusCode);
    }
}
