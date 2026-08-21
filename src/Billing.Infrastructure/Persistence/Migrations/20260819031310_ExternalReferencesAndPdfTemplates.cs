using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalReferencesAndPdfTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "external_system",
                table: "documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "external_reference",
                table: "documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_entity",
                table: "documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_id",
                table: "documents",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pdf_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    primary_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    footer_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    commercial_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    logo_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_templates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_external_system_external_entity_external_id",
                table: "documents",
                columns: new[] { "external_system", "external_entity", "external_id" });

            migrationBuilder.CreateIndex(
                name: "IX_pdf_templates_code",
                table: "pdf_templates",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pdf_templates");

            migrationBuilder.DropIndex(
                name: "IX_documents_external_system_external_entity_external_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "external_entity",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "external_id",
                table: "documents");

            migrationBuilder.AlterColumn<string>(
                name: "external_system",
                table: "documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "external_reference",
                table: "documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
