using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Migracja danych: etapy formacji zależą teraz od ścieżki. Istniejące sprawy (dane testowe) nie są mapowane,
    /// tylko wracają na pierwszy etap swojej ścieżki; Absolwenci zostają Absolwentami (etap istnieje na każdej ścieżce).
    /// 3 = Graduate, 6 = Prekatechumenate (Chrzest, ścieżka 0), 4 = Evangelization (pozostałe ścieżki).
    /// </summary>
    public partial class ResetDokCaseStages : Migration
    {
        public const string ResetSql =
            "UPDATE DokCases SET Stage = CASE WHEN Stage = 3 THEN 3 WHEN Path = 0 THEN 6 ELSE 4 END";

        // Powrót do czterech dawnych etapów: absolwent zostaje, pozostali dostają Zgłoszenie (0).
        public const string RestoreSql = "UPDATE DokCases SET Stage = CASE WHEN Stage = 3 THEN 3 ELSE 0 END";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(ResetSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RestoreSql);
    }
}
