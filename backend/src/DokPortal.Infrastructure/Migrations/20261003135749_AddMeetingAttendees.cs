using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingAttendees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CatechistPersonId",
                table: "Meetings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MeetingAttendees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DokCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsAttended = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAttendees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAttendees_DokCases_DokCaseId",
                        column: x => x.DokCaseId,
                        principalTable: "DokCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingAttendees_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_CatechistPersonId",
                table: "Meetings",
                column: "CatechistPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendees_DokCaseId",
                table: "MeetingAttendees",
                column: "DokCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendees_MeetingId_DokCaseId",
                table: "MeetingAttendees",
                columns: new[] { "MeetingId", "DokCaseId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_People_CatechistPersonId",
                table: "Meetings",
                column: "CatechistPersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_People_CatechistPersonId",
                table: "Meetings");

            migrationBuilder.DropTable(
                name: "MeetingAttendees");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_CatechistPersonId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "CatechistPersonId",
                table: "Meetings");
        }
    }
}
