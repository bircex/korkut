using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenancyRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "matters",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matters", x => x.id);
                    table.UniqueConstraint("AK_matters_office_id_id", x => new { x.office_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "offices",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "matter_hearings",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matter_hearings", x => x.id);
                    table.ForeignKey(
                        name: "FK_matter_hearings_matters_office_id_matter_id",
                        columns: x => new { x.office_id, x.matter_id },
                        principalSchema: "korkut",
                        principalTable: "matters",
                        principalColumns: new[] { "office_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_matter_hearings_office_matter",
                schema: "korkut",
                table: "matter_hearings",
                columns: new[] { "office_id", "matter_id" });

            migrationBuilder.CreateIndex(
                name: "ix_matters_office",
                schema: "korkut",
                table: "matters",
                column: "office_id");

            // --- Tenancy: least-privileged app role + RLS (hand-written; KDM-012) ---
            // korkut_app is NOT a table owner and NOT BYPASSRLS, so it is fully subject
            // to the policies below. It has no LOGIN/password here — those are
            // provisioned by ops (never a secret in the repo).
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'korkut_app') THEN
                        CREATE ROLE korkut_app NOLOGIN;
                    END IF;
                END $$;
                GRANT USAGE ON SCHEMA korkut TO korkut_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.matters TO korkut_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.matter_hearings TO korkut_app;
                GRANT SELECT ON korkut.offices TO korkut_app;");

            // RLS + FORCE (owner is subject too). current_setting(..., true) returns NULL
            // when no tenant context is set -> no row matches -> read/write is blocked.
            foreach (var table in new[] { "matters", "matter_hearings" })
            {
                migrationBuilder.Sql($@"
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
                name: "matter_hearings",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "offices",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "matters",
                schema: "korkut");
        }
    }
}
