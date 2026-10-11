using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telegram_bots",
                schema: "korkut",
                columns: table => new
                {
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_cipher = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    bot_username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    consent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consent_by = table.Column<Guid>(type: "uuid", nullable: true),
                    update_offset = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_bots", x => x.office_id);
                    table.ForeignKey(
                        name: "FK_telegram_bots_offices_office_id",
                        column: x => x.office_id,
                        principalSchema: "korkut",
                        principalTable: "offices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telegram_links",
                schema: "korkut",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<long>(type: "bigint", nullable: true),
                    telegram_username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    code_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_links", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_telegram_links_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "korkut",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_links_chat_id",
                schema: "korkut",
                table: "telegram_links",
                column: "chat_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_links_code_hash",
                schema: "korkut",
                table: "telegram_links",
                column: "code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_links_office_id",
                schema: "korkut",
                table: "telegram_links",
                column: "office_id");

            // Hand-maintained security appendix (same convention as InitialCreate): standard
            // tenant tables, full DML for the app role, RLS keyed on app.current_office.
            foreach (var table in new[] { "telegram_bots", "telegram_links" })
            {
                migrationBuilder.Sql($@"
                    GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.{table} TO korkut_app;
                    ALTER TABLE korkut.{table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE korkut.{table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON korkut.{table}
                        USING (office_id = current_setting('app.current_office', true)::uuid)
                        WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telegram_bots",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "telegram_links",
                schema: "korkut");
        }
    }
}
