using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLetterTemplates : Migration
    {
        /// <summary>Edycja wzorów pism dla dyrektorów (role z zapisanymi uprawnieniami; Administrator ma wszystko).</summary>
        public const string GrantSql = @"
INSERT INTO RolePermissions (RoleName, Permission)
SELECT g.RoleName, g.Permission
FROM (
    SELECT 'DyrektorSKSP' AS RoleName, 'DocumentTemplates.Manage' AS Permission
    UNION ALL SELECT 'DyrektorDOK', 'DocumentTemplates.Manage'
) g
WHERE EXISTS (SELECT 1 FROM RolePermissions r WHERE r.RoleName = g.RoleName)
  AND NOT EXISTS (
      SELECT 1 FROM RolePermissions x
      WHERE x.RoleName = g.RoleName AND x.Permission = g.Permission)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Template",
                table: "GeneratedDocuments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<Guid>(
                name: "LetterTemplateId",
                table: "GeneratedDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RenderedBody",
                table: "GeneratedDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "GeneratedDocuments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseLetterhead",
                table: "GeneratedDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Letterheads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    HeaderText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FooterText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Place = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LogoBlobPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LogoContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Letterheads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LetterTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UseLetterhead = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LegacyType = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetterTemplates", x => x.Id);
                });
            migrationBuilder.Sql(GrantSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM RolePermissions WHERE Permission = 'DocumentTemplates.Manage'");
            migrationBuilder.DropTable(
                name: "Letterheads");

            migrationBuilder.DropTable(
                name: "LetterTemplates");

            migrationBuilder.DropColumn(
                name: "LetterTemplateId",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "RenderedBody",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "UseLetterhead",
                table: "GeneratedDocuments");

            migrationBuilder.AlterColumn<int>(
                name: "Template",
                table: "GeneratedDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
