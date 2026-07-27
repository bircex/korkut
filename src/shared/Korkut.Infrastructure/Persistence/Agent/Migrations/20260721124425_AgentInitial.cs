using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Agent.Migrations
{
    /// <inheritdoc />
    public partial class AgentInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "korkut_agent");

            migrationBuilder.CreateTable(
                name: "chat_messages",
                schema: "korkut_agent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                schema: "korkut_agent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_versions",
                schema: "korkut_agent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_md = table.Column<string>(type: "text", nullable: false),
                    author = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "generated_documents",
                schema: "korkut_agent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content_md = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pending_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pending_content_md = table.Column<string>(type: "text", nullable: true),
                    pending_summary = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_conversation",
                schema: "korkut_agent",
                table: "chat_messages",
                columns: new[] { "office_id", "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_office_updated",
                schema: "korkut_agent",
                table: "conversations",
                columns: new[] { "office_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_document_versions_document",
                schema: "korkut_agent",
                table: "document_versions",
                columns: new[] { "office_id", "document_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_generated_documents_conversation",
                schema: "korkut_agent",
                table: "generated_documents",
                columns: new[] { "office_id", "conversation_id" },
                unique: true);

            // The least-privileged app role (created cluster-wide by the operational
            // TenancyRls migration) can use this schema; RLS + FORCE + tenant policy on
            // every table, keyed on the shared app.current_office GUC (same as operational).
            migrationBuilder.Sql("GRANT USAGE ON SCHEMA korkut_agent TO korkut_app;");
            foreach (var table in new[] { "conversations", "chat_messages", "generated_documents", "document_versions" })
            {
                migrationBuilder.Sql($@"
                    GRANT SELECT, INSERT, UPDATE, DELETE ON korkut_agent.{table} TO korkut_app;
                    ALTER TABLE korkut_agent.{table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE korkut_agent.{table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON korkut_agent.{table}
                        USING (office_id = current_setting('app.current_office', true)::uuid)
                        WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_messages",
                schema: "korkut_agent");

            migrationBuilder.DropTable(
                name: "conversations",
                schema: "korkut_agent");

            migrationBuilder.DropTable(
                name: "document_versions",
                schema: "korkut_agent");

            migrationBuilder.DropTable(
                name: "generated_documents",
                schema: "korkut_agent");
        }
    }
}
