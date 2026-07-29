using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResetTokenResolver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A password-reset link carries an OPAQUE token, not the office. Resolving
            // which tenant it belongs to must cross RLS (there is no session yet), so it
            // goes through a narrow SECURITY DEFINER function owned by the BYPASSRLS
            // korkut_resolver role — the same sanctioned pattern as sign-in resolution.
            // Returns the office/user only for an active (unexpired, unused) token.
            migrationBuilder.Sql(@"
                -- The resolver role owns the function and reads the table under its own
                -- (BYPASSRLS) identity, so it needs table-level SELECT.
                GRANT SELECT ON korkut.password_reset_tokens TO korkut_resolver;

                CREATE FUNCTION korkut.resolve_reset_token(p_hash text, p_now timestamptz)
                RETURNS TABLE(office_id uuid, user_id uuid)
                LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                    SELECT office_id, user_id
                    FROM korkut.password_reset_tokens
                    WHERE token_hash = p_hash AND used_at IS NULL AND expires_at > p_now
                    LIMIT 1;
                $fn$;
                ALTER FUNCTION korkut.resolve_reset_token(text, timestamptz) OWNER TO korkut_resolver;
                REVOKE ALL ON FUNCTION korkut.resolve_reset_token(text, timestamptz) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION korkut.resolve_reset_token(text, timestamptz) TO korkut_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS korkut.resolve_reset_token(text, timestamptz);");
        }
    }
}
