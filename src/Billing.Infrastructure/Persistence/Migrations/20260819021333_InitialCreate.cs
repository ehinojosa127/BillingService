using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    external_system = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    requested_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    series = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_series", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    series = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    issue_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    operation_type_code = table.Column<string>(type: "text", nullable: false),
                    payment_form = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    sunat_status = table.Column<int>(type: "integer", nullable: false),
                    issuer_ruc = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    issuer_legal_name = table.Column<string>(type: "text", nullable: false),
                    issuer_trade_name = table.Column<string>(type: "text", nullable: false),
                    issuer_address_line = table.Column<string>(type: "text", nullable: false),
                    issuer_ubigeo = table.Column<string>(type: "text", nullable: false),
                    issuer_department = table.Column<string>(type: "text", nullable: false),
                    issuer_province = table.Column<string>(type: "text", nullable: false),
                    issuer_district = table.Column<string>(type: "text", nullable: false),
                    issuer_country_code = table.Column<string>(type: "text", nullable: false),
                    issuer_urbanization = table.Column<string>(type: "text", nullable: true),
                    issuer_establishment_code = table.Column<string>(type: "text", nullable: false),
                    issuer_email = table.Column<string>(type: "text", nullable: true),
                    issuer_phone = table.Column<string>(type: "text", nullable: true),
                    recipient_identity_type = table.Column<string>(type: "text", nullable: false),
                    recipient_identity_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    recipient_address_line = table.Column<string>(type: "text", nullable: true),
                    recipient_email = table.Column<string>(type: "text", nullable: true),
                    taxable_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    exempt_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unaffected_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    free_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    export_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    igv_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    line_extension_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tax_inclusive_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payable_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    charge_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    amount_in_words = table.Column<string>(type: "text", nullable: false),
                    digest_value = table.Column<string>(type: "text", nullable: true),
                    observation = table.Column<string>(type: "text", nullable: true),
                    external_system = table.Column<string>(type: "text", nullable: true),
                    external_reference = table.Column<string>(type: "text", nullable: true),
                    requested_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    transfer_reason_code = table.Column<string>(type: "text", nullable: true),
                    transport_mode_code = table.Column<string>(type: "text", nullable: true),
                    transfer_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    gross_weight_kg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    package_count = table.Column<int>(type: "integer", nullable: true),
                    origin_address_line = table.Column<string>(type: "text", nullable: true),
                    origin_ubigeo = table.Column<string>(type: "text", nullable: true),
                    origin_department = table.Column<string>(type: "text", nullable: true),
                    origin_province = table.Column<string>(type: "text", nullable: true),
                    origin_district = table.Column<string>(type: "text", nullable: true),
                    destination_address_line = table.Column<string>(type: "text", nullable: true),
                    destination_ubigeo = table.Column<string>(type: "text", nullable: true),
                    destination_department = table.Column<string>(type: "text", nullable: true),
                    destination_province = table.Column<string>(type: "text", nullable: true),
                    destination_district = table.Column<string>(type: "text", nullable: true),
                    carrier_ruc = table.Column<string>(type: "text", nullable: true),
                    carrier_name = table.Column<string>(type: "text", nullable: true),
                    vehicle_plate = table.Column<string>(type: "text", nullable: true),
                    driver_license = table.Column<string>(type: "text", nullable: true),
                    driver_document_type = table.Column<string>(type: "text", nullable: true),
                    driver_document_number = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    response_payload = table.Column<string>(type: "text", nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "issuers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruc = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    address_line = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ubigeo = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    urbanization = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    establishment_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issuers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    unit_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    affectation_code = table.Column<string>(type: "text", nullable: false),
                    taxable_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    igv_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_items_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_references",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    related_document_type_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    series = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    reason_description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_references", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_references_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    ticket = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    response_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    error_kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_submissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_submissions_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "generated_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_generated_files_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_document_id",
                table: "audit_logs",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_occurred_at",
                table: "audit_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_document_items_document_id",
                table: "document_items",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_references_document_id",
                table: "document_references",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_series_document_type_code_series",
                table: "document_series",
                columns: new[] { "document_type_code", "series" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_submissions_document_id",
                table: "document_submissions",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_document_type_code_series_number",
                table: "documents",
                columns: new[] { "document_type_code", "series", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_external_system_external_reference",
                table: "documents",
                columns: new[] { "external_system", "external_reference" });

            migrationBuilder.CreateIndex(
                name: "IX_generated_files_document_id",
                table: "generated_files",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_key",
                table: "idempotency_records",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_issuers_ruc",
                table: "issuers",
                column: "ruc",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "document_items");

            migrationBuilder.DropTable(
                name: "document_references");

            migrationBuilder.DropTable(
                name: "document_series");

            migrationBuilder.DropTable(
                name: "document_submissions");

            migrationBuilder.DropTable(
                name: "generated_files");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "issuers");

            migrationBuilder.DropTable(
                name: "documents");
        }
    }
}
