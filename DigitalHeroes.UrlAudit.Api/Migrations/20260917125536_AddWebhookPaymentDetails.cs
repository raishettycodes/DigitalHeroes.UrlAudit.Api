using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalHeroes.UrlAudit.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookPaymentDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RazorpayOrderId",
                table: "PaymentWebhookEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazorpayPaymentId",
                table: "PaymentWebhookEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "PaymentWebhookEvents",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RazorpayOrderId",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropColumn(
                name: "RazorpayPaymentId",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "PaymentWebhookEvents");
        }
    }
}
