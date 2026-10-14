using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMailingQueueAndOptOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MailingOptOutAtUtc",
                table: "People",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailingUnsubscribeToken",
                table: "People",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MailingDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AttemptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailingDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailingDeliveries_MailingCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "MailingCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_People_MailingUnsubscribeToken",
                table: "People",
                column: "MailingUnsubscribeToken",
                unique: true,
                filter: "[MailingUnsubscribeToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MailingDeliveries_AttemptedAtUtc",
                table: "MailingDeliveries",
                column: "AttemptedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MailingDeliveries_CampaignId",
                table: "MailingDeliveries",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_MailingDeliveries_Status_Position",
                table: "MailingDeliveries",
                columns: new[] { "Status", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailingDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_People_MailingUnsubscribeToken",
                table: "People");

            migrationBuilder.DropColumn(
                name: "MailingOptOutAtUtc",
                table: "People");

            migrationBuilder.DropColumn(
                name: "MailingUnsubscribeToken",
                table: "People");
        }
    }
}
