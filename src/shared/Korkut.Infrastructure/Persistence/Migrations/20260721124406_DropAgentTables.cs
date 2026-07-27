using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropAgentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // conversations + chat_messages stay in the operational DB (chat is a general
            // feature, not agent plumbing). Only the generated drafts + versions move out to
            // the Agent DB (schema korkut_agent).
            migrationBuilder.DropTable(
                name: "document_versions",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "generated_documents",
                schema: "korkut");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_versions",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    content_md = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "generated_documents",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_md = table.Column<string>(type: "text", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pending_content_md = table.Column<string>(type: "text", nullable: true),
                    pending_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pending_summary = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_versions_document",
                schema: "korkut",
                table: "document_versions",
                columns: new[] { "office_id", "document_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_generated_documents_conversation",
                schema: "korkut",
                table: "generated_documents",
                columns: new[] { "office_id", "conversation_id" },
                unique: true);
        }
    }
}
