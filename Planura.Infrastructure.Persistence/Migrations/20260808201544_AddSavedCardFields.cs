using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planura.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedCardFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "remainder_charged_at",
                table: "payments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remainder_failure_reason",
                table: "payments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remainder_gateway_reference",
                table: "payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "saved_payment_method_id",
                table: "payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_customer_id",
                table: "clients",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "remainder_charged_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "remainder_failure_reason",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "remainder_gateway_reference",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "saved_payment_method_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "stripe_customer_id",
                table: "clients");
        }
    }
}
