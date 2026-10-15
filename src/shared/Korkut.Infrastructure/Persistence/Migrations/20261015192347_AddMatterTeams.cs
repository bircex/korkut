using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatterTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "korkut",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "korkut",
                table: "deadlines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "korkut",
                table: "correspondence",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "korkut",
                table: "communication_notes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "matter_members",
                schema: "korkut",
                columns: table => new
                {
                    matter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_lead = table.Column<bool>(type: "boolean", nullable: false),
                    added_by = table.Column<Guid>(type: "uuid", nullable: true),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matter_members", x => new { x.matter_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_matter_members_matters_office_id_matter_id",
                        columns: x => new { x.office_id, x.matter_id },
                        principalSchema: "korkut",
                        principalTable: "matters",
                        principalColumns: new[] { "office_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_matter_members_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "korkut",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_matter_members_office_id_matter_id",
                schema: "korkut",
                table: "matter_members",
                columns: new[] { "office_id", "matter_id" });

            migrationBuilder.CreateIndex(
                name: "IX_matter_members_user_id",
                schema: "korkut",
                table: "matter_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_matter_members_office_user",
                schema: "korkut",
                table: "matter_members",
                columns: new[] { "office_id", "user_id" });

            // Hand-maintained security appendix (same convention as InitialCreate): a standard
            // tenant table, full DML for the app role, RLS keyed on app.current_office.
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.matter_members TO korkut_app;
                ALTER TABLE korkut.matter_members ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut.matter_members FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut.matter_members
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");

            // Matters opened before teams existed get the office's owners as their team, so nothing
            // disappears for the people running the office (everyone else is added per matter).
            migrationBuilder.Sql(@"
                INSERT INTO korkut.matter_members (matter_id, user_id, office_id, is_lead, added_by, added_at)
                SELECT m.id, u.id, m.office_id, true, NULL, now()
                FROM korkut.matters m
                JOIN korkut.users u ON u.office_id = m.office_id AND u.role = 'owner'
                ON CONFLICT DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "matter_members",
                schema: "korkut");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "korkut",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "korkut",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "korkut",
                table: "correspondence");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "korkut",
                table: "communication_notes");
        }
    }
}
