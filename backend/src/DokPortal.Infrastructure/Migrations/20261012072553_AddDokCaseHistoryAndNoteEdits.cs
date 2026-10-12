using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDokCaseHistoryAndNoteEdits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "PastoralNotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DokCaseEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DokCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    FromValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ToValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PerformedByEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DokCaseEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DokCaseEvents_DokCases_DokCaseId",
                        column: x => x.DokCaseId,
                        principalTable: "DokCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DokCaseEvents_DokCaseId_Sequence",
                table: "DokCaseEvents",
                columns: new[] { "DokCaseId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DokCaseEvents");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "PastoralNotes");
        }
    }
}
