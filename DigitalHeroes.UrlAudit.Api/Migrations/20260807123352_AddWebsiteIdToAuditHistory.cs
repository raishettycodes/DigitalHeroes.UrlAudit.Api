using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalHeroes.UrlAudit.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWebsiteIdToAuditHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WebsiteId",
                table: "AuditHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AuditHistories_WebsiteId",
                table: "AuditHistories",
                column: "WebsiteId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditHistories_Websites_WebsiteId",
                table: "AuditHistories",
                column: "WebsiteId",
                principalTable: "Websites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditHistories_Websites_WebsiteId",
                table: "AuditHistories");

            migrationBuilder.DropIndex(
                name: "IX_AuditHistories_WebsiteId",
                table: "AuditHistories");

            migrationBuilder.DropColumn(
                name: "WebsiteId",
                table: "AuditHistories");
        }
    }
}
