using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Migracja danych: Dyrektor DOK może udzielać posłania, więc istniejąca rola DyrektorDOK (jeśli ma już zapisane uprawnienia)
    /// dostaje podgląd, zarządzanie i eksport katechistów (misji). Seeder wypełnia uprawnienia tylko przy pustej tabeli.
    /// </summary>
    public partial class GrantMissionsToDyrektorDok : Migration
    {
        public const string GrantSql = @"
INSERT INTO RolePermissions (RoleName, Permission)
SELECT 'DyrektorDOK', p.Permission
FROM (SELECT 'Missions.View' AS Permission UNION ALL SELECT 'Missions.Manage' UNION ALL SELECT 'Missions.Export') p
WHERE EXISTS (SELECT 1 FROM RolePermissions r WHERE r.RoleName = 'DyrektorDOK')
  AND NOT EXISTS (
      SELECT 1 FROM RolePermissions x
      WHERE x.RoleName = 'DyrektorDOK' AND x.Permission = p.Permission)";

        public const string RevokeSql =
            "DELETE FROM RolePermissions WHERE RoleName = 'DyrektorDOK' AND Permission IN ('Missions.View', 'Missions.Manage', 'Missions.Export')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(GrantSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RevokeSql);
    }
}
