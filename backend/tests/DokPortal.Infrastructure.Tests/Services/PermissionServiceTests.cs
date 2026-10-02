using DokPortal.Application.Permissions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class PermissionServiceTests
{
    private static (PermissionService Service, AppDbContext Db) Create()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        return (new PermissionService(db, new MemoryCache(new MemoryCacheOptions())), db);
    }

    private static async Task Grant(AppDbContext db, string role, params string[] permissions)
    {
        foreach (var permission in permissions)
        {
            db.RolePermissions.Add(new RolePermission { RoleName = role, Permission = permission });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Administrator_GetsEntireCatalog_WithoutAnyRows()
    {
        var (service, _) = Create();

        var permissions = await service.GetPermissionsForRolesAsync(new[] { AppRoles.Administrator }, default);

        Assert.Equal(PermissionCatalog.AllNames.Count, permissions.Count);
    }

    [Fact]
    public async Task MultipleRoles_GetUnionOfTheirPermissions_AndUnknownRoleGetsNone()
    {
        var (service, db) = Create();
        await Grant(db, AppRoles.KatechistaProwadzacy, Permissions.MeetingsView);
        await Grant(db, AppRoles.DyrektorDOK, Permissions.MeetingsManage);

        var union = await service.GetPermissionsForRolesAsync(new[] { AppRoles.KatechistaProwadzacy, AppRoles.DyrektorDOK }, default);
        var none = await service.GetPermissionsForRolesAsync(new[] { "NieistniejacaRola" }, default);

        Assert.Equal(new[] { Permissions.MeetingsManage, Permissions.MeetingsView }, union.OrderBy(x => x));
        Assert.Empty(none);
    }

    [Fact]
    public async Task UpdateRolePermissions_ReplacesGrants_AndInvalidatesCache()
    {
        var (service, db) = Create();
        await Grant(db, AppRoles.KatechistaProwadzacy, Permissions.MeetingsView);
        var before = await service.GetPermissionsForRolesAsync(new[] { AppRoles.KatechistaProwadzacy }, default);
        Assert.Contains(Permissions.MeetingsView, before);

        await service.UpdateRolePermissionsAsync(AppRoles.KatechistaProwadzacy, new[] { Permissions.CandidatesView }, default);

        var after = await service.GetPermissionsForRolesAsync(new[] { AppRoles.KatechistaProwadzacy }, default);
        Assert.Equal(new[] { Permissions.CandidatesView }, after.ToArray());
        Assert.Equal(1, await db.RolePermissions.CountAsync());
    }

    [Fact]
    public async Task UpdateRolePermissions_WithEmptyList_RemovesAllGrantsOfTheRole()
    {
        var (service, db) = Create();
        await Grant(db, AppRoles.Superwizor, Permissions.SupervisionsView, Permissions.SupervisionsManage);

        await service.UpdateRolePermissionsAsync(AppRoles.Superwizor, Array.Empty<string>(), default);

        Assert.Empty(await service.GetPermissionsForRolesAsync(new[] { AppRoles.Superwizor }, default));
    }

    [Theory]
    [InlineData("Administrator", "People.Manage")]
    [InlineData("NieistniejacaRola", "People.Manage")]
    [InlineData("DyrektorDOK", "People.Fly")]
    public async Task UpdateRolePermissions_RejectsAdministratorUnknownRoleAndUnknownPermission(string role, string permission)
    {
        var (service, _) = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateRolePermissionsAsync(role, new[] { permission }, default));
    }

    [Fact]
    public async Task GetMatrix_ReturnsCatalogEditableRolesAndGrantsIncludingEmptyOnes()
    {
        var (service, db) = Create();
        await Grant(db, AppRoles.DyrektorDOK, Permissions.DokCasesView);

        var matrix = await service.GetMatrixAsync(default);

        Assert.Equal(PermissionCatalog.All.Count, matrix.Permissions.Count);
        Assert.DoesNotContain(AppRoles.Administrator, matrix.Roles);
        Assert.Equal(AppRoles.All.Length - 1, matrix.Roles.Count);
        Assert.Equal(new[] { Permissions.DokCasesView }, matrix.Grants[AppRoles.DyrektorDOK].ToArray());
        Assert.Empty(matrix.Grants[AppRoles.Biskup]);
    }
}
