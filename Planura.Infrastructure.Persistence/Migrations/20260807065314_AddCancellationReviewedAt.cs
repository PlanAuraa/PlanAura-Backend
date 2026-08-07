using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planura.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationReviewedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancellation_reviewed_at",
                table: "booking_requests",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_reviewed_at",
                table: "booking_requests");
        }
    }
}
