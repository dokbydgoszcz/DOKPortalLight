using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoBlobPath",
                table: "People",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "People",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhotoUpdatedAtUtc",
                table: "People",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoBlobPath",
                table: "People");

            migrationBuilder.DropColumn(
                name: "PhotoContentType",
                table: "People");

            migrationBuilder.DropColumn(
                name: "PhotoUpdatedAtUtc",
                table: "People");
        }
    }
}
