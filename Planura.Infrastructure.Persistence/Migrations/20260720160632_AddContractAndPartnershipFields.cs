using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planura.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContractAndPartnershipFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "partnership_agreement_generated_at",
                table: "vendors",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "partnership_agreement_id",
                table: "vendors",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "partnership_agreement_url",
                table: "vendors",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_document_url",
                table: "booking_requests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "contract_generated_at",
                table: "booking_requests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_id",
                table: "booking_requests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "partnership_agreement_generated_at",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "partnership_agreement_id",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "partnership_agreement_url",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "contract_document_url",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "contract_generated_at",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "contract_id",
                table: "booking_requests");
        }
    }
}
