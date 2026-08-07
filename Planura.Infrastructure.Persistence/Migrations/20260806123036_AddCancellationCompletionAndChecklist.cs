using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planura.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationCompletionAndChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "awaiting_confirmation_since",
                table: "booking_requests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "booking_requests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cancellation_refund_amount",
                table: "booking_requests",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cancellation_refund_percent",
                table: "booking_requests",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancellation_requested_at",
                table: "booking_requests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_review_notes",
                table: "booking_requests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refund_status",
                table: "booking_requests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.CreateTable(
                name: "event_plan_checklist_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    service_category_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_plan_checklist_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_event_plan_checklist_items_event_plans_event_plan_id",
                        column: x => x.event_plan_id,
                        principalTable: "event_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_plan_checklist_items_service_categories_service_category_id",
                        column: x => x.service_category_id,
                        principalTable: "service_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_event_plan_checklist_items_event_plan_id_service_category_id",
                table: "event_plan_checklist_items",
                columns: new[] { "event_plan_id", "service_category_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_plan_checklist_items_service_category_id",
                table: "event_plan_checklist_items",
                column: "service_category_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_plan_checklist_items");

            migrationBuilder.DropColumn(
                name: "awaiting_confirmation_since",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "cancellation_refund_amount",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "cancellation_refund_percent",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "cancellation_requested_at",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "cancellation_review_notes",
                table: "booking_requests");

            migrationBuilder.DropColumn(
                name: "refund_status",
                table: "booking_requests");
        }
    }
}
