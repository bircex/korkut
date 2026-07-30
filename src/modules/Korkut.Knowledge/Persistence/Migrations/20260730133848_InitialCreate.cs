using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Korkut.Knowledge.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "korkut_knowledge");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "chunks",
                schema: "korkut_knowledge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    embedding_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    embedding = table.Column<Vector>(type: "vector", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chunks_document",
                schema: "korkut_knowledge",
                table: "chunks",
                columns: new[] { "office_id", "document_id", "ordinal" });

            // Tenant isolation on the korkut_knowledge schema, keyed on the shared app.current_office
            // GUC. korkut_app is created by the operational migration; guard for order-independence.
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'korkut_app') THEN
                        CREATE ROLE korkut_app NOLOGIN;
                    END IF;
                END $$;
                GRANT USAGE ON SCHEMA korkut_knowledge TO korkut_app;");
            foreach (var table in new[] { "chunks" })
            {
                migrationBuilder.Sql($@"
                    GRANT SELECT, INSERT, UPDATE, DELETE ON korkut_knowledge.{table} TO korkut_app;
                    ALTER TABLE korkut_knowledge.{table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE korkut_knowledge.{table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON korkut_knowledge.{table}
                        USING (office_id = current_setting('app.current_office', true)::uuid)
                        WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chunks",
                schema: "korkut_knowledge");
        }
    }
}
