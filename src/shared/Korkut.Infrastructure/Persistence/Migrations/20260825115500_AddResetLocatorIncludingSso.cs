using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResetLocatorIncludingSso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Password-reset must locate a user by e-mail before any session exists, exactly like
            // korkut.resolve_users_by_email — but WITHOUT the "google_sub IS NULL" filter, so a
            // Google-linked account (no local password yet) can still be sent a reset link and set
            // a password. Sign-in keeps using the password-only resolver, so SSO accounts remain
            // non-password-authenticatable until a password is deliberately set here.
            //
            // Same sanctioned RLS-bypass pattern as the other resolvers: SECURITY DEFINER, owned by
            // the dedicated BYPASSRLS role korkut_resolver, EXECUTE granted only to korkut_app.
            migrationBuilder.Sql(@"
                GRANT USAGE, CREATE ON SCHEMA korkut TO korkut_resolver;

                CREATE FUNCTION korkut.resolve_all_users_by_email(p_email text)
                RETURNS TABLE(id uuid, office_id uuid, email text)
                LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                    SELECT id, office_id, email FROM korkut.users WHERE email = p_email;
                $fn$;
                ALTER FUNCTION korkut.resolve_all_users_by_email(text) OWNER TO korkut_resolver;
                REVOKE ALL ON FUNCTION korkut.resolve_all_users_by_email(text) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION korkut.resolve_all_users_by_email(text) TO korkut_app;

                REVOKE CREATE ON SCHEMA korkut FROM korkut_resolver;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS korkut.resolve_all_users_by_email(text);");
        }
    }
}
