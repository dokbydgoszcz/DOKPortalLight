using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ManualFormationYears : Migration
    {
        // Rok formacji przestaje zależeć od kalendarza (wrzesień), tylko jest zapisany i zmieniany ręcznie.
        // Kolumna FormationStartYear (rok rozpoczęcia) staje się FormationYear: najpierw zawiera jeszcze rok rozpoczęcia.
        public static string ToManualYearsSql(int academicYearStart) =>
            "UPDATE Candidates SET " +
            $"IsFormationCompleted = CASE WHEN {academicYearStart} - FormationYear + 1 > 3 THEN 1 ELSE 0 END, " +
            "FormationYearSinceUtc = UpdatedAtUtc, " +
            "FormationYear = CASE " +
            $"WHEN {academicYearStart} - FormationYear + 1 < 1 THEN 1 " +
            $"WHEN {academicYearStart} - FormationYear + 1 > 3 THEN 3 " +
            $"ELSE {academicYearStart} - FormationYear + 1 END";

        // Powrót: z roku formacji (1-3) i znacznika ukończenia z powrotem na rok rozpoczęcia.
        public static string ToStartYearsSql(int academicYearStart) =>
            "UPDATE Candidates SET FormationYear = CASE WHEN IsFormationCompleted = 1 " +
            $"THEN {academicYearStart} - 3 ELSE {academicYearStart} - FormationYear + 1 END";

        private static int CurrentAcademicYearStart()
        {
            var today = DateTime.UtcNow;
            return today.Month >= 9 ? today.Year : today.Year - 1;
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FormationStartYear",
                table: "Candidates",
                newName: "FormationYear");

            migrationBuilder.AddColumn<DateTime>(
                name: "FormationYearSinceUtc",
                table: "Candidates",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsFormationCompleted",
                table: "Candidates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(ToManualYearsSql(CurrentAcademicYearStart()));

            migrationBuilder.CreateTable(
                name: "CandidateFormationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    FromYear = table.Column<int>(type: "int", nullable: true),
                    ToYear = table.Column<int>(type: "int", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PerformedByEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateFormationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateFormationEvents_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateFormationEvents_CandidateId_Sequence",
                table: "CandidateFormationEvents",
                columns: new[] { "CandidateId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateFormationEvents");

            migrationBuilder.Sql(ToStartYearsSql(CurrentAcademicYearStart()));

            migrationBuilder.DropColumn(
                name: "FormationYearSinceUtc",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "IsFormationCompleted",
                table: "Candidates");

            migrationBuilder.RenameColumn(
                name: "FormationYear",
                table: "Candidates",
                newName: "FormationStartYear");
        }
    }
}
