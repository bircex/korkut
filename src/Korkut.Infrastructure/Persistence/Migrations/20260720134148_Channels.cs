using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Channels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channels",
                schema: "korkut",
                columns: table => new
                {
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    from_address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    from_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    base_url = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    encrypted_secret = table.Column<string>(type: "text", nullable: true),
                    linked = table.Column<bool>(type: "boolean", nullable: false),
                    linked_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    link_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channels", x => new { x.office_id, x.kind });
                });

            // RLS + FORCE + app-role grants. Channel rows hold ENCRYPTED provider secrets;
            // tenant isolation keeps one office's credentials invisible to another.
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON korkut.channels TO korkut_app;
                ALTER TABLE korkut.channels ENABLE ROW LEVEL SECURITY;
                ALTER TABLE korkut.channels FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON korkut.channels
                    USING (office_id = current_setting('app.current_office', true)::uuid)
                    WITH CHECK (office_id = current_setting('app.current_office', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channels",
                schema: "korkut");
        }
    }
}
