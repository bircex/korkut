using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionRefresh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family",
                schema: "korkut",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ux_refresh_tokens_hash",
                schema: "korkut",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            // RLS + FORCE + app-role grants for refresh tokens (KDM-018).
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.refresh_tokens TO korkut_app;
                ALTER TABLE korkut.refresh_tokens ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut.refresh_tokens FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut.refresh_tokens
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");

            // Refresh tokens are pre-session (office unknown until lookup): issue/rotate/
            // revoke run in atomic SECURITY DEFINER functions owned by korkut_resolver
            // (BYPASSRLS). Rotation locks the row FOR UPDATE and, on reuse of a
            // used/revoked token, revokes the ENTIRE family — theft detection.
            migrationBuilder.Sql(@"
                GRANT INSERT, SELECT, UPDATE ON korkut.refresh_tokens TO korkut_resolver;

                CREATE FUNCTION korkut.issue_refresh_token(
                    p_id uuid, p_family uuid, p_office uuid, p_user uuid, p_hash text,
                    p_expires timestamptz, p_now timestamptz)
                RETURNS void LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                    INSERT INTO korkut.refresh_tokens(id, family_id, office_id, user_id, token_hash, expires_at, created_at)
                    VALUES (p_id, p_family, p_office, p_user, p_hash, p_expires, p_now);
                $fn$;

                CREATE FUNCTION korkut.rotate_refresh_token(
                    p_hash text, p_new_id uuid, p_new_hash text, p_expires timestamptz, p_now timestamptz)
                RETURNS TABLE(r_status text, r_office uuid, r_user uuid, r_family uuid)
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = korkut AS $fn$
                DECLARE r korkut.refresh_tokens%ROWTYPE;
                BEGIN
                    SELECT * INTO r FROM korkut.refresh_tokens WHERE token_hash = p_hash FOR UPDATE;
                    IF NOT FOUND THEN
                        RETURN QUERY SELECT 'invalid'::text, NULL::uuid, NULL::uuid, NULL::uuid; RETURN;
                    END IF;
                    IF r.revoked_at IS NOT NULL OR r.used_at IS NOT NULL THEN
                        UPDATE korkut.refresh_tokens SET revoked_at = p_now
                            WHERE family_id = r.family_id AND revoked_at IS NULL;
                        RETURN QUERY SELECT 'reuse_revoked'::text, r.office_id, r.user_id, r.family_id; RETURN;
                    END IF;
                    IF r.expires_at <= p_now THEN
                        RETURN QUERY SELECT 'expired'::text, r.office_id, r.user_id, r.family_id; RETURN;
                    END IF;
                    UPDATE korkut.refresh_tokens SET used_at = p_now WHERE id = r.id;
                    INSERT INTO korkut.refresh_tokens(id, family_id, office_id, user_id, token_hash, expires_at, created_at)
                        VALUES (p_new_id, r.family_id, r.office_id, r.user_id, p_new_hash, p_expires, p_now);
                    RETURN QUERY SELECT 'rotated'::text, r.office_id, r.user_id, r.family_id;
                END;
                $fn$;

                CREATE FUNCTION korkut.revoke_refresh_family(p_family uuid, p_now timestamptz)
                RETURNS void LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                    UPDATE korkut.refresh_tokens SET revoked_at = p_now
                    WHERE family_id = p_family AND revoked_at IS NULL;
                $fn$;

                ALTER FUNCTION korkut.issue_refresh_token(uuid, uuid, uuid, uuid, text, timestamptz, timestamptz) OWNER TO korkut_resolver;
                ALTER FUNCTION korkut.rotate_refresh_token(text, uuid, text, timestamptz, timestamptz) OWNER TO korkut_resolver;
                ALTER FUNCTION korkut.revoke_refresh_family(uuid, timestamptz) OWNER TO korkut_resolver;

                REVOKE ALL ON FUNCTION korkut.issue_refresh_token(uuid, uuid, uuid, uuid, text, timestamptz, timestamptz) FROM PUBLIC;
                REVOKE ALL ON FUNCTION korkut.rotate_refresh_token(text, uuid, text, timestamptz, timestamptz) FROM PUBLIC;
                REVOKE ALL ON FUNCTION korkut.revoke_refresh_family(uuid, timestamptz) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION korkut.issue_refresh_token(uuid, uuid, uuid, uuid, text, timestamptz, timestamptz) TO korkut_app;
                GRANT EXECUTE ON FUNCTION korkut.rotate_refresh_token(text, uuid, text, timestamptz, timestamptz) TO korkut_app;
                GRANT EXECUTE ON FUNCTION korkut.revoke_refresh_family(uuid, timestamptz) TO korkut_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "korkut");
        }
    }
}
