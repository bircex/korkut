using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminConsole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_office_models",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_office_models", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "admin_recovery_codes",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    code_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_recovery_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "admin_totp",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    encrypted_secret = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_used_step = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_totp", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_admin_office_models_office",
                schema: "korkut",
                table: "admin_office_models",
                column: "office_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_admin_recovery_email_hash",
                schema: "korkut",
                table: "admin_recovery_codes",
                columns: new[] { "email", "code_hash" });

            migrationBuilder.CreateIndex(
                name: "ux_admin_totp_email",
                schema: "korkut",
                table: "admin_totp",
                column: "email",
                unique: true);

            // Hand-maintained security appendix (same convention as InitialCreate): the admin
            // console tables are cross-tenant (no office_id, no RLS) — the app role gets narrow
            // row privileges only; ownership stays with the migrator.
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE ON korkut.admin_totp TO korkut_app;
                GRANT SELECT, INSERT, UPDATE ON korkut.admin_recovery_codes TO korkut_app;
                GRANT SELECT, INSERT, UPDATE ON korkut.admin_office_models TO korkut_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_office_models",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "admin_recovery_codes",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "admin_totp",
                schema: "korkut");
        }
    }
}
