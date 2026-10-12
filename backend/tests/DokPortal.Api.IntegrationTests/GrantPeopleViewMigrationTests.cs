using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Migrations;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>People.View dostają tylko role, które już zarządzają osobami – raz.</summary>
public class GrantPeopleViewMigrationTests
{
    [Fact]
    public async Task GrantSql_GivesPeopleViewToRolesManagingPeople_Once()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.RolePermissions.AddRange(
            new RolePermission { RoleName = "DyrektorDOK", Permission = "People.Manage" },
            new RolePermission { RoleName = "Wlasna", Permission = "People.Manage" },
            new RolePermission { RoleName = "Wlasna", Permission = "People.View" },
            new RolePermission { RoleName = "KatechistaProwadzacy", Permission = "DokCases.View" });
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(AddUserDeactivationAndPeopleView.GrantSql);
        await db.Database.ExecuteSqlRawAsync(AddUserDeactivationAndPeopleView.GrantSql);

        var viewers = await db.RolePermissions.AsNoTracking().Where(p => p.Permission == "People.View").Select(p => p.RoleName).ToListAsync();
        Assert.Equal(new[] { "DyrektorDOK", "Wlasna" }, viewers.OrderBy(r => r));
    }
}
