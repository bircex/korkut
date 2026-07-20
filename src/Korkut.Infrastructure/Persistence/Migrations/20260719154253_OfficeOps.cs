using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfficeOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "communication_notes",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_notes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deadlines",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    reminder_days_before = table.Column<int>(type: "integer", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deadlines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_comm_notes_office_matter",
                schema: "korkut",
                table: "communication_notes",
                columns: new[] { "office_id", "matter_id" });

            migrationBuilder.CreateIndex(
                name: "ix_deadlines_office_due",
                schema: "korkut",
                table: "deadlines",
                columns: new[] { "office_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_office_status",
                schema: "korkut",
                table: "tasks",
                columns: new[] { "office_id", "status" });

            // RLS + FORCE + app-role grants for the office-operations tables (KDM-024).
            foreach (var table in new[] { "tasks", "deadlines", "communication_notes" })
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
                name: "communication_notes",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "deadlines",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "tasks",
                schema: "korkut");
        }
    }
}
