using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonSex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Sex",
                table: "People",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sex",
                table: "People");
        }
    }
}
