using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfficeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "memory_items",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "office_persona",
                schema: "korkut",
                columns: table => new
                {
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    expertise = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    extra_rules = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_office_persona", x => x.office_id);
                });

            migrationBuilder.CreateTable(
                name: "style_profiles",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    letterhead = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    signature = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    tone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    boilerplate = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_style_profiles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_memory_items_office_status",
                schema: "korkut",
                table: "memory_items",
                columns: new[] { "office_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_style_profiles_office_scope_user",
                schema: "korkut",
                table: "style_profiles",
                columns: new[] { "office_id", "scope", "user_id" },
                unique: true);

            // RLS + FORCE + app-role grants for the office-settings tables.
            foreach (var table in new[] { "office_persona", "style_profiles", "memory_items" })
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
                name: "memory_items",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "office_persona",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "style_profiles",
                schema: "korkut");
        }
    }
}
