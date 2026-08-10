using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planura.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorPayoutRecordedByAdminFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_vendor_payouts_recorded_by_admin_id",
                table: "vendor_payouts",
                column: "recorded_by_admin_id");

            migrationBuilder.AddForeignKey(
                name: "fk_vendor_payouts_users_recorded_by_admin_id",
                table: "vendor_payouts",
                column: "recorded_by_admin_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vendor_payouts_users_recorded_by_admin_id",
                table: "vendor_payouts");

            migrationBuilder.DropIndex(
                name: "ix_vendor_payouts_recorded_by_admin_id",
                table: "vendor_payouts");
        }
    }
}
