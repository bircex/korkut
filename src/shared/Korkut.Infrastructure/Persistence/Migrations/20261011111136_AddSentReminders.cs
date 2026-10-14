using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSentReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sent_reminders",
                schema: "korkut",
                columns: table => new
                {
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sent_reminders", x => new { x.office_id, x.key });
                    table.ForeignKey(
                        name: "FK_sent_reminders_offices_office_id",
                        column: x => x.office_id,
                        principalSchema: "korkut",
                        principalTable: "offices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Hand-maintained security appendix (same convention as InitialCreate): a standard
            // tenant table, full DML for the app role, RLS keyed on app.current_office.
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.sent_reminders TO korkut_app;
                ALTER TABLE korkut.sent_reminders ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut.sent_reminders FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut.sent_reminders
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sent_reminders",
                schema: "korkut");
        }
    }
}
