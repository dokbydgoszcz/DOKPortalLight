using DokPortal.Application.Permissions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class PermissionServiceTests
{
    private static (PermissionService Service, AppDbContext Db) Create()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.AddRange(AppRoles.All.Select(r => new IdentityRole(r) { NormalizedName = r.ToUpperInvariant() }));
        db.SaveChanges();
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
        Assert.DoesNotContain(matrix.Roles, r => r.Name == AppRoles.Administrator);
        Assert.Equal(AppRoles.All.Length - 1, matrix.Roles.Count);
        Assert.All(matrix.Roles, r => Assert.True(r.IsSystem));
        Assert.Equal(new[] { Permissions.DokCasesView }, matrix.Grants[AppRoles.DyrektorDOK].ToArray());
        Assert.Empty(matrix.Grants[AppRoles.Biskup]);
    }

    [Fact]
    public async Task CreateRole_AddsCustomRoleWithoutPermissions_AndShowsItInMatrixAfterSystemRoles()
    {
        var (service, _) = Create();

        var created = await service.CreateRoleAsync("  Sekretariat  ", default);

        Assert.Equal("Sekretariat", created.Name);
        Assert.False(created.IsSystem);
        var matrix = await service.GetMatrixAsync(default);
        Assert.Equal("Sekretariat", matrix.Roles.Last().Name);
        Assert.False(matrix.Roles.Last().IsSystem);
        Assert.Empty(matrix.Grants["Sekretariat"]);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("   ")]
    [InlineData("Rola!")]
    [InlineData("-Rola")]
    public async Task CreateRole_RejectsInvalidNames(string name)
    {
        var (service, _) = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRoleAsync(name, default));
    }

    [Fact]
    public async Task CreateRole_RejectsTooLongName()
    {
        var (service, _) = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRoleAsync(new string('a', 51), default));
    }

    [Theory]
    [InlineData("Sekretariat")]
    [InlineData("SEKRETARIAT")]
    [InlineData("biskup")]
    [InlineData("Administrator")]
    public async Task CreateRole_RejectsDuplicatesIgnoringCase_IncludingSystemRoles(string name)
    {
        var (service, _) = Create();
        await service.CreateRoleAsync("Sekretariat", default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRoleAsync(name, default));
    }

    [Fact]
    public async Task UpdateRolePermissions_WorksForCustomRole()
    {
        var (service, _) = Create();
        await service.CreateRoleAsync("Sekretariat", default);

        await service.UpdateRolePermissionsAsync("Sekretariat", new[] { Permissions.CandidatesView }, default);

        var permissions = await service.GetPermissionsForRolesAsync(new[] { "Sekretariat" }, default);
        Assert.Equal(new[] { Permissions.CandidatesView }, permissions.ToArray());
    }

    [Fact]
    public async Task DeleteRole_RemovesCustomRoleAndItsGrants()
    {
        var (service, db) = Create();
        await service.CreateRoleAsync("Sekretariat", default);
        await service.UpdateRolePermissionsAsync("Sekretariat", new[] { Permissions.CandidatesView }, default);

        await service.DeleteRoleAsync("Sekretariat", default);

        Assert.False(await db.Roles.AnyAsync(r => r.Name == "Sekretariat"));
        Assert.False(await db.RolePermissions.AnyAsync(r => r.RoleName == "Sekretariat"));
        Assert.Empty(await service.GetPermissionsForRolesAsync(new[] { "Sekretariat" }, default));
        var matrix = await service.GetMatrixAsync(default);
        Assert.DoesNotContain(matrix.Roles, r => r.Name == "Sekretariat");
    }

    [Theory]
    [InlineData("Biskup")]
    [InlineData("Administrator")]
    [InlineData("NieistniejacaRola")]
    public async Task DeleteRole_RejectsSystemAndUnknownRoles(string role)
    {
        var (service, _) = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteRoleAsync(role, default));
    }

    [Fact]
    public async Task DeleteRole_RejectsRoleAssignedToUsers()
    {
        var (service, db) = Create();
        var created = await service.CreateRoleAsync("Sekretariat", default);
        var roleId = (await db.Roles.SingleAsync(r => r.Name == created.Name)).Id;
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "user-1", RoleId = roleId });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteRoleAsync("Sekretariat", default));

        Assert.Contains("1", ex.Message);
        Assert.True(await db.Roles.AnyAsync(r => r.Name == "Sekretariat"));
    }

    [Fact]
    public async Task ListRoleNames_ReturnsSystemRolesInDeclaredOrderThenCustomAlphabetically()
    {
        var (service, _) = Create();
        await service.CreateRoleAsync("Zespół", default);
        await service.CreateRoleAsync("Archiwum", default);

        var names = await service.ListRoleNamesAsync(default);

        Assert.Equal(AppRoles.All.Concat(new[] { "Archiwum", "Zespół" }), names);
    }
}
