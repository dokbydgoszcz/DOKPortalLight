using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Migrations;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>People.View dostają role zarządzające osobami oraz Katechista, Superwizor i Biskup – raz.</summary>
public class GrantPeopleViewMigrationTests
{
    [Fact]
    public async Task GrantSql_GivesPeopleViewToPeopleManagers_AndCatechistSupervisorBishop_Once()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.RolePermissions.AddRange(
            new RolePermission { RoleName = "DyrektorDOK", Permission = "People.Manage" },
            new RolePermission { RoleName = "Wlasna", Permission = "People.Manage" },
            new RolePermission { RoleName = "Wlasna", Permission = "People.View" },
            new RolePermission { RoleName = "KatechistaProwadzacy", Permission = "DokCases.View" },
            new RolePermission { RoleName = "Superwizor", Permission = "DokCases.View" },
            new RolePermission { RoleName = "Inna", Permission = "DokCases.View" });
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(AddUserDeactivationAndPeopleView.GrantSql);
        await db.Database.ExecuteSqlRawAsync(AddUserDeactivationAndPeopleView.GrantSql);

        var viewers = await db.RolePermissions.AsNoTracking().Where(p => p.Permission == "People.View").Select(p => p.RoleName).ToListAsync();
        // Biskupa nie ma w tabeli (seeder nie wypełnił) – nie dostaje wpisu; rola „Inna” bez People.Manage też nie.
        Assert.Equal(new[] { "DyrektorDOK", "KatechistaProwadzacy", "Superwizor", "Wlasna" }, viewers.OrderBy(r => r, StringComparer.Ordinal));
    }
}
