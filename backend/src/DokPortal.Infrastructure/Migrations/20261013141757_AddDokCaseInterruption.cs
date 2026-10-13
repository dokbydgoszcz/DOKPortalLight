using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDokCaseInterruption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "InterruptedOn",
                table: "DokCases",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterruptionReason",
                table: "DokCases",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterruptedOn",
                table: "DokCases");

            migrationBuilder.DropColumn(
                name: "InterruptionReason",
                table: "DokCases");
        }
    }
}
