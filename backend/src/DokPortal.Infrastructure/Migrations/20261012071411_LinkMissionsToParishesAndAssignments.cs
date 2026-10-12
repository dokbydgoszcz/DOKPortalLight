using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LinkMissionsToParishesAndAssignments : Migration
    {
        // Misje, których miejsce posługi to dokładnie nazwa jednej istniejącej parafii, dostają jej identyfikator.
        public const string BackfillMissionParishSql =
            "UPDATE CanonicalMissions SET ParishId = (SELECT p.Id FROM Parishes p WHERE p.Name = CanonicalMissions.ServicePlace AND p.DeletedAtUtc IS NULL) " +
            "WHERE ParishId IS NULL AND ServicePlace <> '' " +
            "AND (SELECT COUNT(*) FROM Parishes p WHERE p.Name = CanonicalMissions.ServicePlace AND p.DeletedAtUtc IS NULL) = 1";

        private const string DeletedPeople = "(SELECT Id FROM People WHERE DeletedAtUtc IS NOT NULL)";
        private const string DeletedParishes = "(SELECT Id FROM Parishes WHERE DeletedAtUtc IS NOT NULL)";
        private const string RepairedBy = "'naprawa danych: osoba/parafia usunięta'";

        /// <summary>
        /// Naprawa danych po dawnym usuwaniu osób i parafii bez sprawdzania zależności (przegląd 2026-10-08, A3).
        /// Dane usuniętej osoby (jej sprawa DOK, kandydatura, misje, funkcja formatora) też są usuwane; katechista usuniętej
        /// osoby z żywymi sprawami wraca (inaczej jego sprawy znikały z list); mentor, właściciel zajęć i powiązanie konta są czyszczone.
        /// Odwołania do usuniętych parafii są czyszczone, a ich zapotrzebowania usuwane. Wyrażenie bieżącej daty podaje wywołujący.
        /// </summary>
        public static string[] RepairSql(string now) => new[]
        {
            $"UPDATE DokCases SET DeletedAtUtc = {now}, DeletedBy = {RepairedBy} WHERE DeletedAtUtc IS NULL AND PersonId IN {DeletedPeople}",
            $"UPDATE People SET DeletedAtUtc = NULL, DeletedBy = NULL WHERE DeletedAtUtc IS NOT NULL AND Id IN (SELECT CatechistPersonId FROM DokCases WHERE DeletedAtUtc IS NULL)",
            $"UPDATE Candidates SET DeletedAtUtc = {now}, DeletedBy = {RepairedBy} WHERE DeletedAtUtc IS NULL AND PersonId IN {DeletedPeople}",
            $"UPDATE CanonicalMissions SET DeletedAtUtc = {now}, DeletedBy = {RepairedBy} WHERE DeletedAtUtc IS NULL AND PersonId IN {DeletedPeople}",
            $"UPDATE Formators SET DeletedAtUtc = {now}, DeletedBy = {RepairedBy} WHERE DeletedAtUtc IS NULL AND PersonId IN {DeletedPeople}",
            $"UPDATE DokCases SET MentorPersonId = NULL WHERE MentorPersonId IN {DeletedPeople}",
            $"UPDATE Meetings SET CatechistPersonId = NULL WHERE CatechistPersonId IN {DeletedPeople}",
            $"UPDATE AspNetUsers SET PersonId = NULL WHERE PersonId IN {DeletedPeople}",
            $"UPDATE People SET ParishId = NULL WHERE ParishId IN {DeletedParishes}",
            $"UPDATE PersonFunctions SET ParishId = NULL WHERE ParishId IN {DeletedParishes}",
            $"UPDATE CanonicalMissions SET ParishId = NULL WHERE ParishId IN {DeletedParishes}",
            $"UPDATE ParishNeeds SET DeletedAtUtc = {now}, DeletedBy = {RepairedBy} WHERE DeletedAtUtc IS NULL AND ParishId IN {DeletedParishes}"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MissionCreatedByAssignment",
                table: "ParishNeedAssignments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "MissionId",
                table: "ParishNeedAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MissionPlaceFilledByAssignment",
                table: "ParishNeedAssignments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParishId",
                table: "CanonicalMissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalMissions_ParishId",
                table: "CanonicalMissions",
                column: "ParishId");

            migrationBuilder.AddForeignKey(
                name: "FK_CanonicalMissions_Parishes_ParishId",
                table: "CanonicalMissions",
                column: "ParishId",
                principalTable: "Parishes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            foreach (var sql in RepairSql("SYSUTCDATETIME()"))
            {
                migrationBuilder.Sql(sql);
            }
            migrationBuilder.Sql(BackfillMissionParishSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CanonicalMissions_Parishes_ParishId",
                table: "CanonicalMissions");

            migrationBuilder.DropIndex(
                name: "IX_CanonicalMissions_ParishId",
                table: "CanonicalMissions");

            migrationBuilder.DropColumn(
                name: "MissionCreatedByAssignment",
                table: "ParishNeedAssignments");

            migrationBuilder.DropColumn(
                name: "MissionId",
                table: "ParishNeedAssignments");

            migrationBuilder.DropColumn(
                name: "MissionPlaceFilledByAssignment",
                table: "ParishNeedAssignments");

            migrationBuilder.DropColumn(
                name: "ParishId",
                table: "CanonicalMissions");
        }
    }
}
