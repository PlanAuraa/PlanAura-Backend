using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planura.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPaymentAndSlotHolds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_vendor_availability_vendor_id_start_at",
                table: "vendor_availability");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hold_expires_at",
                table: "vendor_availability",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "booking_requests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "pending");

            migrationBuilder.AddColumn<string>(
                name: "dispute_status",
                table: "booking_requests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "disputed_at",
                table: "booking_requests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "disputed_by_user_id",
                table: "booking_requests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                table: "booking_requests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unpaid");

            migrationBuilder.AddColumn<string>(
                name: "resolution_notes",
                table: "booking_requests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "resolved_at",
                table: "booking_requests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "resolved_by_admin_id",
                table: "booking_requests",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    booking_request_id = table.Column<long>(type: "bigint", nullable: false),
                    client_id = table.Column<long>(type: "bigint", nullable: false),
                    vendor_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    payment_method = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    gateway_reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    refunded_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    refund_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_booking_requests_booking_request_id",
                        column: x => x.booking_request_id,
                        principalTable: "booking_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_vendor_availability_no_double_hold",
                table: "vendor_availability",
                columns: new[] { "vendor_id", "start_at" },
                unique: true,
                filter: "([status] IN ('Held', 'Booked'))");

            migrationBuilder.CreateIndex(
                name: "ix_booking_requests_disputed_by_user_id",
                table: "booking_requests",
                column: "disputed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_requests_resolved_by_admin_id",
                table: "booking_requests",
                column: "resolved_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_booking_request_id",
                table: "payments",
                column: "booking_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_client_id",
                table: "payments",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_gateway_reference",
                table: "payments",
                column: "gateway_reference");

            migrationBuilder.CreateIndex(
                name: "ix_payments_vendor_id",
                table: "payments",
                column: "vendor_id");

            migrationBuilder.AddForeignKey(
                name: "fk_booking_requests_users_disputed_by_user_id",
                table: "booking_requests",
                column: "disputed_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_booking_requests_users_resolved_by_admin_id",
                table: "booking_requests",
                column: "resolved_by_admin_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_booking_requests_users_disputed_by_user_id",
                table: "booking_requests");

            migrationBuilder.DropForeignKey(
                name: "fk_booking_requests_users_resolved_by_admin_id",
                table: "booking_requests");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropIndex(
                name: "idx_vendor_availability_no_double_hold",
                table: "vendor_availability");

            migrationBuilder.DropIndex(
                name: "ix_booking_requests_disputed_by_user_id",
                table: "booking_requests");

            migrationBuilder.DropIndex(
                name: "ix_booking_requests_resolved_by_admin_id",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "hold_expires_at",
                table: "vendor_availability");

            migrationBuilder.DropColumn(
                name: "dispute_status",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "disputed_at",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "disputed_by_user_id",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "resolution_notes",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "resolved_by_admin_id",
                table: "booking_requests");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "booking_requests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_availability_vendor_id_start_at",
                table: "vendor_availability",
                columns: new[] { "vendor_id", "start_at" });
        }
    }
}
