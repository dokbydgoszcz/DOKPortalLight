using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonFunctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonFunctions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ParishId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InstitutedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonFunctions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonFunctions_Parishes_ParishId",
                        column: x => x.ParishId,
                        principalTable: "Parishes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonFunctions_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonFunctions_ParishId",
                table: "PersonFunctions",
                column: "ParishId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonFunctions_PersonId",
                table: "PersonFunctions",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonFunctions_Type_ParishId",
                table: "PersonFunctions",
                columns: new[] { "Type", "ParishId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonFunctions");
        }
    }
}
