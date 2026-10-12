using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPastoralNoteShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PastoralNoteShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DokCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SharedWithPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PastoralNoteShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PastoralNoteShares_DokCases_DokCaseId",
                        column: x => x.DokCaseId,
                        principalTable: "DokCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PastoralNoteShares_People_SharedWithPersonId",
                        column: x => x.SharedWithPersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PastoralNoteShares_DokCaseId_AuthorUserId_SharedWithPersonId",
                table: "PastoralNoteShares",
                columns: new[] { "DokCaseId", "AuthorUserId", "SharedWithPersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PastoralNoteShares_SharedWithPersonId",
                table: "PastoralNoteShares",
                column: "SharedWithPersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PastoralNoteShares");
        }
    }
}
