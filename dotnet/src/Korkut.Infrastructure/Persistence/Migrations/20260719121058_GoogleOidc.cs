using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GoogleOidc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "google_sub",
                schema: "korkut",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "two_factor_enabled",
                schema: "korkut",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "disabled",
                schema: "korkut",
                table: "offices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ux_users_google_sub",
                schema: "korkut",
                table: "users",
                column: "google_sub",
                unique: true);

            // --- Cross-tenant sign-in resolvers (KDM-016) ---
            // Sign-in must find a user before the office (and thus the tenant context)
            // is known. This is the ONLY sanctioned RLS exception: narrow SECURITY
            // DEFINER functions owned by a dedicated BYPASSRLS role, with EXECUTE
            // granted to the app role. The app role itself never bypasses RLS.
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'korkut_resolver') THEN
                        CREATE ROLE korkut_resolver NOLOGIN BYPASSRLS;
                    END IF;
                END $$;
                GRANT USAGE ON SCHEMA korkut TO korkut_resolver;
                GRANT SELECT, UPDATE ON korkut.users TO korkut_resolver;
                GRANT SELECT ON korkut.offices TO korkut_resolver;

                CREATE FUNCTION korkut.resolve_user_by_google_sub(p_sub text)
                RETURNS TABLE(id uuid, office_id uuid, email text)
                LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                    SELECT id, office_id, email FROM korkut.users WHERE google_sub = p_sub;
                $fn$;

                CREATE FUNCTION korkut.resolve_users_by_email(p_email text)
                RETURNS TABLE(id uuid, office_id uuid, email text)
                LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                    SELECT id, office_id, email FROM korkut.users
                    WHERE email = p_email AND google_sub IS NULL;
                $fn$;

                CREATE FUNCTION korkut.link_google_sub(p_user_id uuid, p_sub text)
                RETURNS void LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                    UPDATE korkut.users SET google_sub = p_sub WHERE id = p_user_id;
                $fn$;

                CREATE FUNCTION korkut.is_office_disabled(p_office uuid)
                RETURNS boolean LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                    SELECT COALESCE((SELECT disabled FROM korkut.offices WHERE id = p_office), true);
                $fn$;

                CREATE FUNCTION korkut.user_has_two_factor(p_user uuid)
                RETURNS boolean LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                    SELECT COALESCE((SELECT two_factor_enabled FROM korkut.users WHERE id = p_user), false);
                $fn$;

                -- Own the functions with the BYPASSRLS role so DEFINER actually bypasses RLS.
                ALTER FUNCTION korkut.resolve_user_by_google_sub(text) OWNER TO korkut_resolver;
                ALTER FUNCTION korkut.resolve_users_by_email(text) OWNER TO korkut_resolver;
                ALTER FUNCTION korkut.link_google_sub(uuid, text) OWNER TO korkut_resolver;
                ALTER FUNCTION korkut.is_office_disabled(uuid) OWNER TO korkut_resolver;
                ALTER FUNCTION korkut.user_has_two_factor(uuid) OWNER TO korkut_resolver;

                -- Only these narrow functions are executable by the app role.
                REVOKE ALL ON FUNCTION korkut.resolve_user_by_google_sub(text) FROM PUBLIC;
                REVOKE ALL ON FUNCTION korkut.resolve_users_by_email(text) FROM PUBLIC;
                REVOKE ALL ON FUNCTION korkut.link_google_sub(uuid, text) FROM PUBLIC;
                REVOKE ALL ON FUNCTION korkut.is_office_disabled(uuid) FROM PUBLIC;
                REVOKE ALL ON FUNCTION korkut.user_has_two_factor(uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION korkut.resolve_user_by_google_sub(text) TO korkut_app;
                GRANT EXECUTE ON FUNCTION korkut.resolve_users_by_email(text) TO korkut_app;
                GRANT EXECUTE ON FUNCTION korkut.link_google_sub(uuid, text) TO korkut_app;
                GRANT EXECUTE ON FUNCTION korkut.is_office_disabled(uuid) TO korkut_app;
                GRANT EXECUTE ON FUNCTION korkut.user_has_two_factor(uuid) TO korkut_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_users_google_sub",
                schema: "korkut",
                table: "users");

            migrationBuilder.DropColumn(
                name: "google_sub",
                schema: "korkut",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_enabled",
                schema: "korkut",
                table: "users");

            migrationBuilder.DropColumn(
                name: "disabled",
                schema: "korkut",
                table: "offices");
        }
    }
}
