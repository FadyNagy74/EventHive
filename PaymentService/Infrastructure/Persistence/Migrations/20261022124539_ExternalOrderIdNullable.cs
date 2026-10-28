using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Migrations
{
    /// <inheritdoc />
    public partial class ExternalOrderIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Payments_ExternalOrderId",
                table: "Payments");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalOrderId",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "UQ_Payments_ExternalOrderId",
                table: "Payments",
                column: "ExternalOrderId",
                unique: true,
                filter: "[ExternalOrderId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Payments_ExternalOrderId",
                table: "Payments");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalOrderId",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Payments_ExternalOrderId",
                table: "Payments",
                column: "ExternalOrderId",
                unique: true);
        }
    }
}
