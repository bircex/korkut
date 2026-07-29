using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfficeTotp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "require_two_factor",
                schema: "korkut",
                table: "offices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "recovery_codes",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recovery_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_totp",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encrypted_secret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_used_step = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_totp", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recovery_codes_office_user",
                schema: "korkut",
                table: "recovery_codes",
                columns: new[] { "office_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_user_totp_office_user",
                schema: "korkut",
                table: "user_totp",
                columns: new[] { "office_id", "user_id" },
                unique: true);

            // RLS + FORCE + app-role grants for the 2FA tables (KDM-017).
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.user_totp TO korkut_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.recovery_codes TO korkut_app;");
            foreach (var table in new[] { "user_totp", "recovery_codes" })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE korkut.{table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE korkut.{table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON korkut.{table}
                        USING (office_id = current_setting('app.current_office', true)::uuid)
                        WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");
            }

            // Office-level mandatory-2FA policy resolver (used by the login gate, KDM-016).
            migrationBuilder.Sql(@"
                CREATE FUNCTION korkut.office_requires_two_factor(p_office uuid)
                RETURNS boolean LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                    SELECT COALESCE((SELECT require_two_factor FROM korkut.offices WHERE id = p_office), false);
                $fn$;
                ALTER FUNCTION korkut.office_requires_two_factor(uuid) OWNER TO korkut_resolver;
                REVOKE ALL ON FUNCTION korkut.office_requires_two_factor(uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION korkut.office_requires_two_factor(uuid) TO korkut_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recovery_codes",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "user_totp",
                schema: "korkut");

            migrationBuilder.DropColumn(
                name: "require_two_factor",
                schema: "korkut",
                table: "offices");
        }
    }
}
