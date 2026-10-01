using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Supervisions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Supervisions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "People",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "People",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "ParishNeeds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ParishNeeds",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Parishes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Parishes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Meetings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Meetings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Formators",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Formators",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "DokCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "DokCases",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "CanonicalMissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "CanonicalMissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Candidates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Candidates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "BudgetEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "BudgetEntries",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Supervisions");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Supervisions");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "People");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "People");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "ParishNeeds");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ParishNeeds");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Parishes");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Parishes");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Formators");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Formators");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "DokCases");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "DokCases");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "CanonicalMissions");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "CanonicalMissions");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "BudgetEntries");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "BudgetEntries");
        }
    }
}
