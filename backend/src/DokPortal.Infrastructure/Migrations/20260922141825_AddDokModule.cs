using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDokModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DokCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Path = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    CatechistPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastMeetingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DokCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DokCases_People_CatechistPersonId",
                        column: x => x.CatechistPersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DokCases_People_MentorPersonId",
                        column: x => x.MentorPersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DokCases_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Supervisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Institution = table.Column<int>(type: "int", nullable: false),
                    GroupLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SupervisionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AttendeesCount = table.Column<int>(type: "int", nullable: true),
                    ExpectedCount = table.Column<int>(type: "int", nullable: true),
                    Topic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Conclusion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Supervisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DokCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsProvided = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseDocuments_DokCases_DokCaseId",
                        column: x => x.DokCaseId,
                        principalTable: "DokCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DokCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GroupLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MeetingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsAttended = table.Column<bool>(type: "bit", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meetings_DokCases_DokCaseId",
                        column: x => x.DokCaseId,
                        principalTable: "DokCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PastoralNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DokCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PastoralNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PastoralNotes_DokCases_DokCaseId",
                        column: x => x.DokCaseId,
                        principalTable: "DokCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseDocuments_DokCaseId",
                table: "CaseDocuments",
                column: "DokCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_DokCases_CatechistPersonId",
                table: "DokCases",
                column: "CatechistPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_DokCases_MentorPersonId",
                table: "DokCases",
                column: "MentorPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_DokCases_PersonId",
                table: "DokCases",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_DokCaseId",
                table: "Meetings",
                column: "DokCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PastoralNotes_DokCaseId",
                table: "PastoralNotes",
                column: "DokCaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseDocuments");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropTable(
                name: "PastoralNotes");

            migrationBuilder.DropTable(
                name: "Supervisions");

            migrationBuilder.DropTable(
                name: "DokCases");
        }
    }
}
