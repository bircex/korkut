using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoleAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "role",
                schema: "korkut",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "office_custom_roles",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    permissions = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_office_custom_roles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_custom_roles_office_key",
                schema: "korkut",
                table: "office_custom_roles",
                columns: new[] { "office_id", "key" },
                unique: true);

            // RLS + FORCE + app-role grants — custom roles (and thus their assignment)
            // are tenant-scoped, so a role can never be defined/used across offices (KDM-019).
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.office_custom_roles TO korkut_app;
                ALTER TABLE korkut.office_custom_roles ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut.office_custom_roles FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut.office_custom_roles
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "office_custom_roles",
                schema: "korkut");

            migrationBuilder.DropColumn(
                name: "role",
                schema: "korkut",
                table: "users");
        }
    }
}
