using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telegram_group_members",
                schema: "korkut",
                columns: table => new
                {
                    chat_id = table.Column<long>(type: "bigint", nullable: false),
                    telegram_user_id = table.Column<long>(type: "bigint", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_group_members", x => new { x.chat_id, x.telegram_user_id });
                });

            migrationBuilder.CreateTable(
                name: "telegram_groups",
                schema: "korkut",
                columns: table => new
                {
                    chat_id = table.Column<long>(type: "bigint", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_by = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_groups", x => x.chat_id);
                    table.ForeignKey(
                        name: "FK_telegram_groups_matters_office_id_matter_id",
                        columns: x => new { x.office_id, x.matter_id },
                        principalSchema: "korkut",
                        principalTable: "matters",
                        principalColumns: new[] { "office_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_group_members_office_id",
                schema: "korkut",
                table: "telegram_group_members",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "IX_telegram_groups_office_id_matter_id",
                schema: "korkut",
                table: "telegram_groups",
                columns: new[] { "office_id", "matter_id" });

            // Hand-maintained security appendix (same convention as InitialCreate): standard tenant
            // tables, full DML for the app role, RLS keyed on app.current_office.
            foreach (var table in new[] { "telegram_groups", "telegram_group_members" })
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
                name: "telegram_group_members",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "telegram_groups",
                schema: "korkut");
        }
    }
}
