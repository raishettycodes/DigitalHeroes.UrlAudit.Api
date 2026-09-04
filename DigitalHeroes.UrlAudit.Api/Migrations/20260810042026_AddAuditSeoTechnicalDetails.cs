using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalHeroes.UrlAudit.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditSeoTechnicalDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ContentLength",
                table: "AuditHistories",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "AuditHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "H1Count",
                table: "AuditHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "H2Count",
                table: "AuditHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HttpVersion",
                table: "AuditHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Images",
                table: "AuditHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImagesWithoutAlt",
                table: "AuditHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRedirect",
                table: "AuditHistories",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSslValid",
                table: "AuditHistories",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "AuditHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedirectLocation",
                table: "AuditHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeoScore",
                table: "AuditHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Server",
                table: "AuditHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "AuditHistories",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentLength",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "H1Count",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "H2Count",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "HttpVersion",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "Images",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "ImagesWithoutAlt",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "IsRedirect",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "IsSslValid",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "RedirectLocation",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "SeoScore",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "Server",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "AuditHistories");
        }
    }
}
