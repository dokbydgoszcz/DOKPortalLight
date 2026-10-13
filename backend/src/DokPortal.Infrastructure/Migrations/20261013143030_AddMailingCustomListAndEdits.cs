using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMailingCustomListAndEdits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "MailingCampaigns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MailingCampaignRecipients",
                columns: table => new
                {
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailingCampaignRecipients", x => new { x.CampaignId, x.PersonId });
                    table.ForeignKey(
                        name: "FK_MailingCampaignRecipients_MailingCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "MailingCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MailingCampaignRecipients_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MailingCampaignRecipients_PersonId",
                table: "MailingCampaignRecipients",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailingCampaignRecipients");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "MailingCampaigns");
        }
    }
}
