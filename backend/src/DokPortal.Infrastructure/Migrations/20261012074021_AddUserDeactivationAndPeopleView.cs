using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Wyłączanie kont oraz nowe uprawnienie People.View (dane osób i imieniny przestają być widoczne dla każdego zalogowanego).
    /// People.View dostają role, które już zarządzają osobami (seeder wypełnia uprawnienia tylko przy pustej tabeli).
    /// </summary>
    public partial class AddUserDeactivationAndPeopleView : Migration
    {
        // People.View: role zarządzające osobami oraz – decyzja właściciela 2026-10-12 – Katechista prowadzący, Superwizor i Biskup.
        public const string GrantSql = @"
INSERT INTO RolePermissions (RoleName, Permission)
SELECT DISTINCT r.RoleName, 'People.View'
FROM (
    SELECT m.RoleName FROM RolePermissions m WHERE m.Permission = 'People.Manage'
    UNION SELECT 'KatechistaProwadzacy' UNION SELECT 'Superwizor' UNION SELECT 'Biskup'
) r
WHERE EXISTS (SELECT 1 FROM RolePermissions e WHERE e.RoleName = r.RoleName)
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleName = r.RoleName AND x.Permission = 'People.View')";

        public const string RevokeSql = "DELETE FROM RolePermissions WHERE Permission = 'People.View'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeactivated",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(GrantSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RevokeSql);
            migrationBuilder.DropColumn(
                name: "IsDeactivated",
                table: "AspNetUsers");
        }
    }
}
