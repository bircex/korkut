using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModelCatalogAndPlatformSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_model_catalog",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    model_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    base_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    max_tokens = table.Column<int>(type: "integer", nullable: true),
                    is_local = table.Column<bool>(type: "boolean", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_model_catalog", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "admin_platform_settings",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_platform_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_admin_model_catalog_key",
                schema: "korkut",
                table: "admin_model_catalog",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_admin_platform_settings_key",
                schema: "korkut",
                table: "admin_platform_settings",
                column: "key",
                unique: true);

            // Hand-maintained security appendix (same convention as AddAdminConsole): both tables are
            // cross-tenant admin config (no office_id, no RLS) — the app role gets narrow row
            // privileges only; ownership stays with the migrator. DELETE is granted on the catalog
            // because an admin can retire a model from the console.
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.admin_model_catalog TO korkut_app;
                GRANT SELECT, INSERT, UPDATE ON korkut.admin_platform_settings TO korkut_app;");

            // Seed the catalog with the providers this build ships adapters for, so the console has
            // something to assign on day one. Model ids mirror the RuntimeConfig env defaults; an
            // admin edits or extends these from the dashboard afterwards. No API keys here — a row is
            // only selectable when its provider's key is present in the environment.
            migrationBuilder.Sql(@"
                INSERT INTO korkut.admin_model_catalog
                    (id, key, label, provider, model_id, base_url, max_tokens, is_local, enabled, sort_order, created_at, updated_at)
                VALUES
                    ('0198a1c0-0001-7000-8000-000000000001', 'deepseek-v3', 'DeepSeek V3', 'deepseek', 'deepseek-chat',
                     'https://api.deepseek.com', 8000, false, true, 10, now(), now()),
                    ('0198a1c0-0001-7000-8000-000000000002', 'gemini-3.5-flash', 'Gemini 3.5 Flash', 'gemini', 'gemini-3.5-flash',
                     'https://generativelanguage.googleapis.com/v1beta/openai', 8000, false, true, 20, now(), now()),
                    ('0198a1c0-0001-7000-8000-000000000003', 'claude', 'Claude', 'claude', 'claude-opus-4-8',
                     'https://api.anthropic.com', 16000, false, true, 30, now(), now()),
                    ('0198a1c0-0001-7000-8000-000000000004', 'gpt-4o', 'OpenAI GPT-4o', 'gpt', 'gpt-4o',
                     'https://api.openai.com/v1', 4096, false, true, 40, now(), now()),
                    ('0198a1c0-0001-7000-8000-000000000005', 'groq-llama-3.3', 'Groq Llama 3.3 70B', 'groq', 'llama-3.3-70b-versatile',
                     'https://api.groq.com/openai/v1', 4096, false, true, 50, now(), now())
                ON CONFLICT DO NOTHING;");

            // The default-model dial starts UNSET on purpose: an unset default falls through to the
            // env-driven specialty router, so upgrading this migration changes no routing behaviour.
            // The row exists so the setting is discoverable before an admin ever touches it.
            migrationBuilder.Sql(@"
                INSERT INTO korkut.admin_platform_settings (id, key, value, updated_at)
                VALUES ('0198a1c0-0002-7000-8000-000000000001', 'default_model_key', NULL, now())
                ON CONFLICT DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_model_catalog",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "admin_platform_settings",
                schema: "korkut");
        }
    }
}
