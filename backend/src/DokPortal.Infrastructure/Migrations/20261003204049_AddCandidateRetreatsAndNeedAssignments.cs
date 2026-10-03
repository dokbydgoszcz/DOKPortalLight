using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateRetreatsAndNeedAssignments : Migration
    {
        // Przenosi dotychczasowe dane do nowych tabel, zanim stare kolumny znikną. Id nowego rekordu = Id rekordu źródłowego
        // (wiersz źródłowy daje najwyżej jeden nowy), dzięki czemu SQL jest przenośny i można go sprawdzić testem.
        public const string RetreatsUpSql =
            "INSERT INTO CandidateRetreats (Id, CandidateId, [Year], IsCompleted) " +
            "SELECT Id, Id, [Year], 1 FROM Candidates WHERE IsRetreatCompleted = 1";

        public const string AssignmentsUpSql =
            "INSERT INTO ParishNeedAssignments (Id, ParishNeedId, PersonId, AssignedAtUtc) " +
            "SELECT Id, Id, AssignedPersonId, COALESCE(AssignedAtUtc, CreatedAtUtc) FROM ParishNeeds WHERE AssignedPersonId IS NOT NULL";

        // Powrót: flaga = rekolekcje zaliczone w bieżącym roku kandydata; przypisana osoba = pierwsza skierowana.
        public const string RetreatsDownSql =
            "UPDATE Candidates SET IsRetreatCompleted = 1 WHERE EXISTS (" +
            "SELECT 1 FROM CandidateRetreats r WHERE r.CandidateId = Candidates.Id AND r.[Year] = Candidates.[Year] AND r.IsCompleted = 1)";

        public const string AssignmentsDownSql =
            "UPDATE ParishNeeds SET " +
            "AssignedPersonId = (SELECT a.PersonId FROM ParishNeedAssignments a WHERE a.ParishNeedId = ParishNeeds.Id " +
            "AND a.AssignedAtUtc = (SELECT MIN(b.AssignedAtUtc) FROM ParishNeedAssignments b WHERE b.ParishNeedId = ParishNeeds.Id)), " +
            "AssignedAtUtc = (SELECT MIN(c.AssignedAtUtc) FROM ParishNeedAssignments c WHERE c.ParishNeedId = ParishNeeds.Id) " +
            "WHERE EXISTS (SELECT 1 FROM ParishNeedAssignments d WHERE d.ParishNeedId = ParishNeeds.Id)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateRetreats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateRetreats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateRetreats_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParishNeedAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParishNeedId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParishNeedAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParishNeedAssignments_ParishNeeds_ParishNeedId",
                        column: x => x.ParishNeedId,
                        principalTable: "ParishNeeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParishNeedAssignments_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateRetreats_CandidateId_Year",
                table: "CandidateRetreats",
                columns: new[] { "CandidateId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParishNeedAssignments_ParishNeedId_PersonId",
                table: "ParishNeedAssignments",
                columns: new[] { "ParishNeedId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParishNeedAssignments_PersonId",
                table: "ParishNeedAssignments",
                column: "PersonId");

            migrationBuilder.Sql(RetreatsUpSql);
            migrationBuilder.Sql(AssignmentsUpSql);

            migrationBuilder.DropForeignKey(
                name: "FK_ParishNeeds_People_AssignedPersonId",
                table: "ParishNeeds");

            migrationBuilder.DropIndex(
                name: "IX_ParishNeeds_AssignedPersonId",
                table: "ParishNeeds");

            migrationBuilder.DropColumn(
                name: "AssignedAtUtc",
                table: "ParishNeeds");

            migrationBuilder.DropColumn(
                name: "AssignedPersonId",
                table: "ParishNeeds");

            migrationBuilder.DropColumn(
                name: "IsRetreatCompleted",
                table: "Candidates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAtUtc",
                table: "ParishNeeds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedPersonId",
                table: "ParishNeeds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetreatCompleted",
                table: "Candidates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(RetreatsDownSql);
            migrationBuilder.Sql(AssignmentsDownSql);

            migrationBuilder.CreateIndex(
                name: "IX_ParishNeeds_AssignedPersonId",
                table: "ParishNeeds",
                column: "AssignedPersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_ParishNeeds_People_AssignedPersonId",
                table: "ParishNeeds",
                column: "AssignedPersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropTable(
                name: "CandidateRetreats");

            migrationBuilder.DropTable(
                name: "ParishNeedAssignments");
        }
    }
}
