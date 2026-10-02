using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Seed;

public class DbSeederPermissionsTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task SeedRolePermissionsAsync_InsertsDefaultGrantsForEveryRole()
    {
        await using var db = CreateContext();

        await DbSeeder.SeedRolePermissionsAsync(db);

        var expected = DefaultRolePermissions.Grants.Sum(g => g.Value.Length);
        Assert.Equal(expected, await db.RolePermissions.CountAsync());
        Assert.True(await db.RolePermissions.AnyAsync(r => r.RoleName == AppRoles.DyrektorDOK && r.Permission == Permissions.DokCasesManage));
        Assert.False(await db.RolePermissions.AnyAsync(r => r.RoleName == AppRoles.Administrator));
    }

    [Fact]
    public async Task SeedRolePermissionsAsync_DoesNotOverwriteExistingRows()
    {
        await using var db = CreateContext();
        db.RolePermissions.Add(new RolePermission { RoleName = AppRoles.KatechistaProwadzacy, Permission = Permissions.MeetingsView });
        await db.SaveChangesAsync();

        await DbSeeder.SeedRolePermissionsAsync(db);

        Assert.Equal(1, await db.RolePermissions.CountAsync());
    }
}
