using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Migracja danych: biblioteka zasobów dla katechistów. Superwizor i Dyrektorzy wgrywają i przeglądają, Katechista prowadzący tylko przegląda.
    /// Dotyczy ról, które mają już zapisane uprawnienia (seeder wypełnia uprawnienia tylko przy pustej tabeli).
    /// </summary>
    public partial class GrantResourcesPermissions : Migration
    {
        public const string GrantSql = @"
INSERT INTO RolePermissions (RoleName, Permission)
SELECT g.RoleName, g.Permission
FROM (
    SELECT 'Superwizor' AS RoleName, 'Resources.View' AS Permission
    UNION ALL SELECT 'Superwizor', 'Resources.Manage'
    UNION ALL SELECT 'DyrektorDOK', 'Resources.View'
    UNION ALL SELECT 'DyrektorDOK', 'Resources.Manage'
    UNION ALL SELECT 'DyrektorSKSP', 'Resources.View'
    UNION ALL SELECT 'DyrektorSKSP', 'Resources.Manage'
    UNION ALL SELECT 'KatechistaProwadzacy', 'Resources.View'
) g
WHERE EXISTS (SELECT 1 FROM RolePermissions r WHERE r.RoleName = g.RoleName)
  AND NOT EXISTS (
      SELECT 1 FROM RolePermissions x
      WHERE x.RoleName = g.RoleName AND x.Permission = g.Permission)";

        public const string RevokeSql =
            "DELETE FROM RolePermissions WHERE Permission IN ('Resources.View', 'Resources.Manage')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(GrantSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RevokeSql);
    }
}
