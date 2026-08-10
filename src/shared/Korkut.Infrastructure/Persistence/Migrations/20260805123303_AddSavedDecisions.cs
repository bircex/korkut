using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_decisions",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    date = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    case_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    decision_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    court = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    chamber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    snippet = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_decisions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_saved_decisions_office_created",
                schema: "korkut",
                table: "saved_decisions",
                columns: new[] { "office_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_saved_decisions_office_source_external",
                schema: "korkut",
                table: "saved_decisions",
                columns: new[] { "office_id", "source", "external_id" },
                unique: true);

            // Hand-maintained security appendix (same convention as InitialCreate): a standard
            // tenant table, full DML for the app role, RLS keyed on app.current_office.
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.saved_decisions TO korkut_app;
                ALTER TABLE korkut.saved_decisions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut.saved_decisions FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut.saved_decisions
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_decisions",
                schema: "korkut");
        }
    }
}
