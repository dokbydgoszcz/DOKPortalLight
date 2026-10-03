using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateFormationCalendar : Migration
    {
        // Dotychczasowa kolumna Year (rok formacji 1-3 "dziś") staje się rokiem rozpoczęcia formacji, liczonym od bieżącego roku szkolnego.
        public static string ToStartYearSql(int academicYearStart) =>
            $"UPDATE Candidates SET FormationStartYear = {academicYearStart} - (FormationStartYear - 1)";

        // Powrót: rok formacji 1-3 z roku rozpoczęcia (po ukończeniu zostaje 3).
        public static string ToFormationYearSql(int academicYearStart) =>
            "UPDATE Candidates SET FormationStartYear = CASE " +
            $"WHEN {academicYearStart} - FormationStartYear + 1 < 1 THEN 1 " +
            $"WHEN {academicYearStart} - FormationStartYear + 1 > 3 THEN 3 " +
            $"ELSE {academicYearStart} - FormationStartYear + 1 END";

        private static int CurrentAcademicYearStart()
        {
            var today = DateTime.UtcNow;
            return today.Month >= 9 ? today.Year : today.Year - 1;
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Year",
                table: "Candidates",
                newName: "FormationStartYear");

            migrationBuilder.Sql(ToStartYearSql(CurrentAcademicYearStart()));

            migrationBuilder.AddColumn<string>(
                name: "FormationStopNote",
                table: "Candidates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFormationStopped",
                table: "Candidates",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormationStopNote",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "IsFormationStopped",
                table: "Candidates");

            migrationBuilder.Sql(ToFormationYearSql(CurrentAcademicYearStart()));

            migrationBuilder.RenameColumn(
                name: "FormationStartYear",
                table: "Candidates",
                newName: "Year");
        }
    }
}
