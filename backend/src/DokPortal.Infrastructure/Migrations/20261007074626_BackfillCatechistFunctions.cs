using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillCatechistFunctions : Migration
    {
        // Katechista stał się funkcją osoby (FunctionType.Catechist = 1): osoby z istniejącą misją kanoniczną dostają ją raz.
        // Wyrażenie na nowy identyfikator podaje wywołujący (SQL Server: NEWID()), żeby zapytanie dało się sprawdzić na SQLite.
        public static string BackfillSql(string newGuid) =>
            "INSERT INTO PersonFunctions (Id, PersonId, Type) " +
            $"SELECT {newGuid}, p.Id, 1 FROM People p " +
            "WHERE p.DeletedAtUtc IS NULL " +
            "AND EXISTS (SELECT 1 FROM CanonicalMissions m WHERE m.PersonId = p.Id AND m.DeletedAtUtc IS NULL) " +
            "AND NOT EXISTS (SELECT 1 FROM PersonFunctions f WHERE f.PersonId = p.Id AND f.Type = 1)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(BackfillSql("NEWID()"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
