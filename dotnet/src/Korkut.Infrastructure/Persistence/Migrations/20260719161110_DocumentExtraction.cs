using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "legal_hold",
                schema: "korkut",
                table: "documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "tombstoned_at",
                schema: "korkut",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_documents_office_id_id",
                schema: "korkut",
                table: "documents",
                columns: new[] { "office_id", "id" });

            migrationBuilder.CreateTable(
                name: "document_extractions",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_version = table.Column<int>(type: "integer", nullable: false),
                    parser_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    page = table.Column<int>(type: "integer", nullable: false),
                    span_index = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    source_checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_object_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_extractions", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_extractions_documents_office_id_document_id",
                        columns: x => new { x.office_id, x.document_id },
                        principalSchema: "korkut",
                        principalTable: "documents",
                        principalColumns: new[] { "office_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_doc_extractions_provenance",
                schema: "korkut",
                table: "document_extractions",
                columns: new[] { "office_id", "document_id", "document_version", "parser_version", "page", "span_index" },
                unique: true);
            // RLS + FORCE + app-role grants for extraction artifacts (KDM-027).
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.document_extractions TO korkut_app;
                ALTER TABLE korkut.document_extractions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut.document_extractions FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut.document_extractions
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_extractions",
                schema: "korkut");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_documents_office_id_id",
                schema: "korkut",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "legal_hold",
                schema: "korkut",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "tombstoned_at",
                schema: "korkut",
                table: "documents");
        }
    }
}
