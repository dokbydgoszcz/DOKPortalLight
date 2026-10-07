using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDokCaseStageSince : Migration
    {
        // Istniejące sprawy: absolwent – od zakończenia, pozostali – od założenia sprawy (stan etapów wyzerowała poprzednia migracja).
        public const string BackfillSql =
            "UPDATE DokCases SET StageSinceUtc = CASE WHEN Stage = 3 THEN COALESCE(CompletedAtUtc, UpdatedAtUtc) ELSE CreatedAtUtc END";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StageSinceUtc",
                table: "DokCases",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql(BackfillSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StageSinceUtc",
                table: "DokCases");
        }
    }
}
