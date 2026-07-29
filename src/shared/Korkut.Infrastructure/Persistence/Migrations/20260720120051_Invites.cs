using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Invites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invites",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invites", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invites_office_email",
                schema: "korkut",
                table: "invites",
                columns: new[] { "office_id", "email" });

            migrationBuilder.CreateIndex(
                name: "ix_invites_token_hash",
                schema: "korkut",
                table: "invites",
                column: "token_hash");

            // RLS + FORCE + app-role grants (an owner manages their office's invites).
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.invites TO korkut_app;
                ALTER TABLE korkut.invites ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut.invites FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut.invites
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);

                -- Accepting an invite happens before any session, so resolving its office
                -- crosses RLS via a narrow BYPASSRLS SECURITY DEFINER function (owned by
                -- korkut_resolver) — the sanctioned pattern. Returns an active invite only.
                GRANT SELECT ON korkut.invites TO korkut_resolver;
                CREATE FUNCTION korkut.resolve_invite_token(p_hash text, p_now timestamptz)
                RETURNS TABLE(id uuid, office_id uuid, email text, role text)
                LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                    SELECT id, office_id, email, role
                    FROM korkut.invites
                    WHERE token_hash = p_hash AND accepted_at IS NULL AND expires_at > p_now
                    LIMIT 1;
                $fn$;
                ALTER FUNCTION korkut.resolve_invite_token(text, timestamptz) OWNER TO korkut_resolver;
                REVOKE ALL ON FUNCTION korkut.resolve_invite_token(text, timestamptz) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION korkut.resolve_invite_token(text, timestamptz) TO korkut_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS korkut.resolve_invite_token(text, timestamptz);");
            migrationBuilder.DropTable(
                name: "invites",
                schema: "korkut");
        }
    }
}
