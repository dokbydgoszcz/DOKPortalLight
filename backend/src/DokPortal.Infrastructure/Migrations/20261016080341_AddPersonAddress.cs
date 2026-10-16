using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressCity",
                table: "People",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressPostalCode",
                table: "People",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressStreet",
                table: "People",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressCity",
                table: "People");

            migrationBuilder.DropColumn(
                name: "AddressPostalCode",
                table: "People");

            migrationBuilder.DropColumn(
                name: "AddressStreet",
                table: "People");
        }
    }
}
