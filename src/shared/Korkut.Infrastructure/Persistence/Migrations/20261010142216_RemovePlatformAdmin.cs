using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlatformAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Korkut is self-hosted, one office per install: the platform-admin console (its TOTP,
            // recovery codes and per-office model bookkeeping) and the self-serve signup are gone.
            // The signup's provisioning functions go with them; the install's office is created by
            // korkut.setup_first_office at first-run setup.
            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS korkut.provision_office(text, text, text, timestamptz);
                DROP FUNCTION IF EXISTS korkut.provision_office_local(text, text, text, text, timestamptz);");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the tables only; the signup provisioning functions are not recreated.
            migrationBuilder.CreateTable(
                name: "admin_office_models",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    model_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    code_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
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
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    encrypted_secret = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    last_used_step = table.Column<long>(type: "bigint", nullable: false)
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
        }
    }
}
