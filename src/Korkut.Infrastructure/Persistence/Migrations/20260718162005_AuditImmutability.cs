using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditImmutability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "korkut",
                table: "matters",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    before_json = table.Column<string>(type: "text", nullable: true),
                    after_json = table.Column<string>(type: "text", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_entity",
                schema: "korkut",
                table: "audit_events",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_office_at",
                schema: "korkut",
                table: "audit_events",
                columns: new[] { "office_id", "at" });

            // --- Append-only audit: RLS + INSERT/SELECT-only grant + immutability trigger (KDM-014) ---
            migrationBuilder.Sql(@"
                ALTER TABLE korkut.audit_events ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut.audit_events FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut.audit_events
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);

                -- The app role can append and read audit, but never mutate it.
                GRANT SELECT, INSERT ON korkut.audit_events TO korkut_app;

                -- Defence in depth: block UPDATE/DELETE for EVERYONE (even a mis-granted
                -- role or the table owner), so the audit trail is truly append-only.
                CREATE OR REPLACE FUNCTION korkut.audit_events_immutable()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_events is append-only (% not allowed)', TG_OP;
                END;
                $$;
                CREATE TRIGGER audit_events_no_mutate
                    BEFORE UPDATE OR DELETE ON korkut.audit_events
                    FOR EACH ROW EXECUTE FUNCTION korkut.audit_events_immutable();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "korkut");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "korkut",
                table: "matters");
        }
    }
}
