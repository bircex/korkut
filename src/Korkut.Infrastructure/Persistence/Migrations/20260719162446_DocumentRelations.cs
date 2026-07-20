using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_relations",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_relations", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_relations_documents_office_id_document_id",
                        columns: x => new { x.office_id, x.document_id },
                        principalSchema: "korkut",
                        principalTable: "documents",
                        principalColumns: new[] { "office_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_doc_relations_target",
                schema: "korkut",
                table: "document_relations",
                columns: new[] { "office_id", "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ux_doc_relations_link",
                schema: "korkut",
                table: "document_relations",
                columns: new[] { "office_id", "document_id", "target_type", "target_id" },
                unique: true);
            // RLS + FORCE + app-role grants for document relations (KDM-028).
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.document_relations TO korkut_app;
                ALTER TABLE korkut.document_relations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut.document_relations FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut.document_relations
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_relations",
                schema: "korkut");
        }
    }
}
