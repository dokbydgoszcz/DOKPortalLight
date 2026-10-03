using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <summary>
    /// Migracja danych: istniejące role, które mają DokCases.View (poza katechistą prowadzącym),
    /// dostają nowe uprawnienie DokCases.ViewAll, żeby ich widoczność spraw DOK się nie zmieniła.
    /// </summary>
    public partial class AddDokCasesViewAll : Migration
    {
        public const string GrantSql = @"
INSERT INTO RolePermissions (RoleName, Permission)
SELECT DISTINCT rp.RoleName, 'DokCases.ViewAll'
FROM RolePermissions rp
WHERE rp.Permission = 'DokCases.View'
  AND rp.RoleName <> 'KatechistaProwadzacy'
  AND NOT EXISTS (
      SELECT 1 FROM RolePermissions x
      WHERE x.RoleName = rp.RoleName AND x.Permission = 'DokCases.ViewAll')";

        public const string RevokeSql = "DELETE FROM RolePermissions WHERE Permission = 'DokCases.ViewAll'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(GrantSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RevokeSql);
    }
}
