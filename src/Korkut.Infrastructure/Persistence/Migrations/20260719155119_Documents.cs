using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Documents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documents",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bucket = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    object_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    original_filename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    retention_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documents_office_matter",
                schema: "korkut",
                table: "documents",
                columns: new[] { "office_id", "matter_id" });

            migrationBuilder.CreateIndex(
                name: "ix_documents_office_status",
                schema: "korkut",
                table: "documents",
                columns: new[] { "office_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_documents_object_key",
                schema: "korkut",
                table: "documents",
                column: "object_key",
                unique: true);
            // RLS + FORCE + app-role grants for documents metadata (KDM-025).
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.documents TO korkut_app;
                ALTER TABLE korkut.documents ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut.documents FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut.documents
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documents",
                schema: "korkut");
        }
    }
}
