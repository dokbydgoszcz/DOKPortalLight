using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseDocumentFileMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlobPath",
                table: "CaseDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "CaseDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "CaseDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "CaseDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAtUtc",
                table: "CaseDocuments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlobPath",
                table: "CaseDocuments");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "CaseDocuments");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "CaseDocuments");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "CaseDocuments");

            migrationBuilder.DropColumn(
                name: "UploadedAtUtc",
                table: "CaseDocuments");
        }
    }
}
