using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Agent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LlmCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "llm_calls",
                schema: "korkut_agent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    model_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(12,6)", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_calls", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_llm_calls_office_created",
                schema: "korkut_agent",
                table: "llm_calls",
                columns: new[] { "office_id", "created_at" });

            // Same tenancy guard as the other agent tables: grant to the app role, FORCE RLS, and a
            // per-office policy on the shared app.current_office GUC (so cost data is office-scoped).
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut_agent.llm_calls TO korkut_app;
                ALTER TABLE korkut_agent.llm_calls ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut_agent.llm_calls FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut_agent.llm_calls
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "llm_calls",
                schema: "korkut_agent");
        }
    }
}
