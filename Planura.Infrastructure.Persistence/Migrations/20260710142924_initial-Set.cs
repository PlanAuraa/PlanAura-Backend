using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planura.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class initialSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_categories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name_en = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    icon_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    full_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    preferred_language = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false, defaultValue: "ar"),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    user_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "bit", nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    security_stamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    phone_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "bit", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "bit", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "bit", nullable: false),
                    access_failed_count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    claim_type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    claim_value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_claims_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    city = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    avatar_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clients", x => x.id);
                    table.ForeignKey(
                        name: "fk_clients_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    data_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_read = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    claim_type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    claim_value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_claims_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    provider_key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    provider_display_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_user_logins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    login_provider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_user_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vendors",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    business_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    business_description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    category_id = table.Column<long>(type: "bigint", nullable: true),
                    city = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    cover_image_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    logo_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    verification_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "unverified"),
                    avg_rating = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    total_reviews = table.Column<int>(type: "int", nullable: false),
                    total_completed_bookings = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendors", x => x.id);
                    table.ForeignKey(
                        name: "fk_vendors_service_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "service_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vendors_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_plans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    client_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    event_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    event_date = table.Column<DateOnly>(type: "date", nullable: true),
                    city = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    guest_count = table.Column<int>(type: "int", nullable: true),
                    budget_total = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    style_notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_plans", x => x.id);
                    table.ForeignKey(
                        name: "fk_event_plans_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "portfolio_links",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vendor_id = table.Column<long>(type: "bigint", nullable: false),
                    platform = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_portfolio_links_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "portfolio_media",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vendor_id = table.Column<long>(type: "bigint", nullable: false),
                    media_type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    file_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    thumbnail_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    file_size_kb = table.Column<int>(type: "int", nullable: true),
                    duration_sec = table.Column<int>(type: "int", nullable: true),
                    display_order = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_media", x => x.id);
                    table.ForeignKey(
                        name: "fk_portfolio_media_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vendor_packages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vendor_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    base_price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "EGP"),
                    max_guests = table.Column<int>(type: "int", nullable: true),
                    includes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendor_packages", x => x.id);
                    table.ForeignKey(
                        name: "fk_vendor_packages_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vendor_verifications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vendor_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    commercial_doc_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    national_id_doc_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    reviewed_by_admin_id = table.Column<long>(type: "bigint", nullable: true),
                    rejection_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    trusted_since = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendor_verifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_vendor_verifications_users_reviewed_by_admin_id",
                        column: x => x.reviewed_by_admin_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vendor_verifications_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_event_visualizations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    prompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    image_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    model_used = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_event_visualizations", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_event_visualizations_event_plans_event_plan_id",
                        column: x => x.event_plan_id,
                        principalTable: "event_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_invitations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    theme = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    image_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    prompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_invitations_event_plans_event_plan_id",
                        column: x => x.event_plan_id,
                        principalTable: "event_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    client_id = table.Column<long>(type: "bigint", nullable: false),
                    vendor_id = table.Column<long>(type: "bigint", nullable: false),
                    vendor_package_id = table.Column<long>(type: "bigint", nullable: true),
                    event_date = table.Column<DateOnly>(type: "date", nullable: false),
                    guest_count = table.Column<int>(type: "int", nullable: true),
                    agreed_price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    client_message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    vendor_response = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    responded_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_requests_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_booking_requests_event_plans_event_plan_id",
                        column: x => x.event_plan_id,
                        principalTable: "event_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_booking_requests_vendor_packages_vendor_package_id",
                        column: x => x.vendor_package_id,
                        principalTable: "vendor_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_booking_requests_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_plan_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    vendor_id = table.Column<long>(type: "bigint", nullable: false),
                    vendor_package_id = table.Column<long>(type: "bigint", nullable: true),
                    estimated_price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_plan_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_event_plan_items_event_plans_event_plan_id",
                        column: x => x.event_plan_id,
                        principalTable: "event_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_plan_items_vendor_packages_vendor_package_id",
                        column: x => x.vendor_package_id,
                        principalTable: "vendor_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_event_plan_items_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vendor_verification_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vendor_verification_id = table.Column<long>(type: "bigint", nullable: false),
                    previous_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    new_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    changed_by_admin_id = table.Column<long>(type: "bigint", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendor_verification_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_vendor_verification_history_users_changed_by_admin_id",
                        column: x => x.changed_by_admin_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vendor_verification_history_vendor_verifications_vendor_verification_id",
                        column: x => x.vendor_verification_id,
                        principalTable: "vendor_verifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    booking_request_id = table.Column<long>(type: "bigint", nullable: false),
                    previous_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    new_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    changed_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_status_history_booking_requests_booking_request_id",
                        column: x => x.booking_request_id,
                        principalTable: "booking_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_booking_status_history_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    booking_request_id = table.Column<long>(type: "bigint", nullable: false),
                    client_id = table.Column<long>(type: "bigint", nullable: false),
                    vendor_id = table.Column<long>(type: "bigint", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: false),
                    comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_hidden = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviews", x => x.id);
                    table.ForeignKey(
                        name: "fk_reviews_booking_requests_booking_request_id",
                        column: x => x.booking_request_id,
                        principalTable: "booking_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviews_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviews_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vendor_availability",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vendor_id = table.Column<long>(type: "bigint", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    booking_request_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendor_availability", x => x.id);
                    table.ForeignKey(
                        name: "fk_vendor_availability_booking_requests_booking_request_id",
                        column: x => x.booking_request_id,
                        principalTable: "booking_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_vendor_availability_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_responses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    review_id = table.Column<long>(type: "bigint", nullable: false),
                    vendor_id = table.Column<long>(type: "bigint", nullable: false),
                    response = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_responses", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_responses_reviews_review_id",
                        column: x => x.review_id,
                        principalTable: "reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_responses_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_event_visualizations_event_plan_id",
                table: "ai_event_visualizations",
                column: "event_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_invitations_event_plan_id",
                table: "ai_invitations",
                column: "event_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_requests_client_id",
                table: "booking_requests",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_requests_event_plan_id",
                table: "booking_requests",
                column: "event_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_requests_status",
                table: "booking_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_booking_requests_vendor_id",
                table: "booking_requests",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_requests_vendor_package_id",
                table: "booking_requests",
                column: "vendor_package_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_status_history_booking_request_id",
                table: "booking_status_history",
                column: "booking_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_status_history_changed_by_user_id",
                table: "booking_status_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_clients_user_id",
                table: "clients",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_plan_items_event_plan_id",
                table: "event_plan_items",
                column: "event_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_plan_items_vendor_id",
                table: "event_plan_items",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_plan_items_vendor_package_id",
                table: "event_plan_items",
                column: "vendor_package_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_plans_client_id",
                table: "event_plans",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_links_vendor_id",
                table: "portfolio_links",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_media_vendor_id",
                table: "portfolio_media",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_responses_review_id",
                table: "review_responses",
                column: "review_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_responses_vendor_id",
                table: "review_responses",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_booking_request_id",
                table: "reviews",
                column: "booking_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_client_id",
                table: "reviews",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_vendor_id",
                table: "reviews",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_claims_role_id",
                table: "role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "role_name_index",
                table: "roles",
                column: "normalized_name",
                unique: true,
                filter: "[normalized_name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_service_categories_slug",
                table: "service_categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_claims_user_id",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_user_id",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "email_index",
                table: "users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "user_name_index",
                table: "users",
                column: "normalized_user_name",
                unique: true,
                filter: "[normalized_user_name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_availability_booking_request_id",
                table: "vendor_availability",
                column: "booking_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_availability_vendor_id_start_at",
                table: "vendor_availability",
                columns: new[] { "vendor_id", "start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_vendor_packages_vendor_id",
                table: "vendor_packages",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_verification_history_changed_by_admin_id",
                table: "vendor_verification_history",
                column: "changed_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_verification_history_vendor_verification_id",
                table: "vendor_verification_history",
                column: "vendor_verification_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_verifications_reviewed_by_admin_id",
                table: "vendor_verifications",
                column: "reviewed_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_verifications_vendor_id",
                table: "vendor_verifications",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendors_category_id",
                table: "vendors",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendors_city",
                table: "vendors",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "ix_vendors_user_id",
                table: "vendors",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vendors_verification_status",
                table: "vendors",
                column: "verification_status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_event_visualizations");

            migrationBuilder.DropTable(
                name: "ai_invitations");

            migrationBuilder.DropTable(
                name: "booking_status_history");

            migrationBuilder.DropTable(
                name: "event_plan_items");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "portfolio_links");

            migrationBuilder.DropTable(
                name: "portfolio_media");

            migrationBuilder.DropTable(
                name: "review_responses");

            migrationBuilder.DropTable(
                name: "role_claims");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "vendor_availability");

            migrationBuilder.DropTable(
                name: "vendor_verification_history");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "vendor_verifications");

            migrationBuilder.DropTable(
                name: "booking_requests");

            migrationBuilder.DropTable(
                name: "event_plans");

            migrationBuilder.DropTable(
                name: "vendor_packages");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "vendors");

            migrationBuilder.DropTable(
                name: "service_categories");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
