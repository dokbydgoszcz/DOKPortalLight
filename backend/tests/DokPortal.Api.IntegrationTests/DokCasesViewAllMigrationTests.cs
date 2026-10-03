using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Migrations;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class DokCasesViewAllMigrationTests
{
    [Fact]
    public async Task GrantSql_AddsViewAllToRolesThatViewCases_ExceptTheCatechist_AndIsIdempotent()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.RolePermissions.AddRange(
            new RolePermission { RoleName = "DyrektorDOK", Permission = Permissions.DokCasesView },
            new RolePermission { RoleName = "Superwizor", Permission = Permissions.DokCasesView },
            new RolePermission { RoleName = "Superwizor", Permission = Permissions.DokCasesViewAll },
            new RolePermission { RoleName = AppRoles.KatechistaProwadzacy, Permission = Permissions.DokCasesView },
            new RolePermission { RoleName = "Sekretariat", Permission = Permissions.DokCasesView },
            new RolePermission { RoleName = "Skarbnik", Permission = Permissions.BudgetDokView });
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(AddDokCasesViewAll.GrantSql);
        await db.Database.ExecuteSqlRawAsync(AddDokCasesViewAll.GrantSql);

        var withViewAll = await db.RolePermissions
            .Where(p => p.Permission == Permissions.DokCasesViewAll)
            .Select(p => p.RoleName).OrderBy(r => r).ToListAsync();
        Assert.Equal(new[] { "DyrektorDOK", "Sekretariat", "Superwizor" }, withViewAll);
    }

    [Fact]
    public async Task RevokeSql_RemovesEveryViewAllGrant()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.RolePermissions.AddRange(
            new RolePermission { RoleName = "DyrektorDOK", Permission = Permissions.DokCasesView },
            new RolePermission { RoleName = "DyrektorDOK", Permission = Permissions.DokCasesViewAll });
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(AddDokCasesViewAll.RevokeSql);

        Assert.Equal(new[] { Permissions.DokCasesView }, await db.RolePermissions.Select(p => p.Permission).ToListAsync());
    }
}
