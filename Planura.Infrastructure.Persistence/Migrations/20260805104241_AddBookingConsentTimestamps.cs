using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planura.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingConsentTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "client_agreed_at",
                table: "booking_requests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "vendor_agreed_at",
                table: "booking_requests",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "client_agreed_at",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "vendor_agreed_at",
                table: "booking_requests");
        }
    }
}
