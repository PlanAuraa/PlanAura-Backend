using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planura.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorVerificationDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "commercial_doc_url",
                table: "vendor_verifications");

            migrationBuilder.DropColumn(
                name: "national_id_doc_url",
                table: "vendor_verifications");

            migrationBuilder.AddColumn<int>(
                name: "vendor_type",
                table: "vendors",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_current",
                table: "vendor_verifications",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "vendor_verification_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vendor_verification_id = table.Column<long>(type: "bigint", nullable: false),
                    document_type = table.Column<int>(type: "int", nullable: false),
                    file_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    original_file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    content_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendor_verification_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_vendor_verification_documents_vendor_verifications_vendor_verification_id",
                        column: x => x.vendor_verification_id,
                        principalTable: "vendor_verifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vendor_verification_documents_vendor_verification_id",
                table: "vendor_verification_documents",
                column: "vendor_verification_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vendor_verification_documents");

            migrationBuilder.DropColumn(
                name: "vendor_type",
                table: "vendors");

            migrationBuilder.DropColumn(
                name: "is_current",
                table: "vendor_verifications");

            migrationBuilder.AddColumn<string>(
                name: "commercial_doc_url",
                table: "vendor_verifications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "national_id_doc_url",
                table: "vendor_verifications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
