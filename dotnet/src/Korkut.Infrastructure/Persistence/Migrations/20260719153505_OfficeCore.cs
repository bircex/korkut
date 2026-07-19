using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfficeCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "case_type",
                schema: "korkut",
                table: "matters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "client_id",
                schema: "korkut",
                table: "matters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "counterparty",
                schema: "korkut",
                table: "matters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "court_id",
                schema: "korkut",
                table: "matters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "court_name_snapshot",
                schema: "korkut",
                table: "matters",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "korkut",
                table: "matters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_no",
                schema: "korkut",
                table: "matters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "opened_at",
                schema: "korkut",
                table: "matters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "korkut",
                table: "matters",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "clients",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
                    table.UniqueConstraint("AK_clients_office_id_id", x => new { x.office_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "courts",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parties",
                schema: "korkut",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parties", x => x.id);
                    table.ForeignKey(
                        name: "FK_parties_matters_office_id_matter_id",
                        columns: x => new { x.office_id, x.matter_id },
                        principalSchema: "korkut",
                        principalTable: "matters",
                        principalColumns: new[] { "office_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_matters_office_id_client_id",
                schema: "korkut",
                table: "matters",
                columns: new[] { "office_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "ix_matters_office_status",
                schema: "korkut",
                table: "matters",
                columns: new[] { "office_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_clients_office_deleted",
                schema: "korkut",
                table: "clients",
                columns: new[] { "office_id", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_courts_office",
                schema: "korkut",
                table: "courts",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "ix_parties_office_matter",
                schema: "korkut",
                table: "parties",
                columns: new[] { "office_id", "matter_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_matters_clients_office_id_client_id",
                schema: "korkut",
                table: "matters",
                columns: new[] { "office_id", "client_id" },
                principalSchema: "korkut",
                principalTable: "clients",
                principalColumns: new[] { "office_id", "id" },
                onDelete: ReferentialAction.SetNull);

            // RLS + FORCE + app-role grants for the new office-core tables (KDM-023).
            foreach (var table in new[] { "clients", "courts", "parties" })
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
            migrationBuilder.DropForeignKey(
                name: "FK_matters_clients_office_id_client_id",
                schema: "korkut",
                table: "matters");

            migrationBuilder.DropTable(
                name: "clients",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "courts",
                schema: "korkut");

            migrationBuilder.DropTable(
                name: "parties",
                schema: "korkut");

            migrationBuilder.DropIndex(
                name: "IX_matters_office_id_client_id",
                schema: "korkut",
                table: "matters");

            migrationBuilder.DropIndex(
                name: "ix_matters_office_status",
                schema: "korkut",
                table: "matters");

            migrationBuilder.DropColumn(
                name: "case_type",
                schema: "korkut",
                table: "matters");

            migrationBuilder.DropColumn(
                name: "client_id",
                schema: "korkut",
                table: "matters");

            migrationBuilder.DropColumn(
                name: "counterparty",
                schema: "korkut",
                table: "matters");

            migrationBuilder.DropColumn(
                name: "court_id",
                schema: "korkut",
                table: "matters");

            migrationBuilder.DropColumn(
                name: "court_name_snapshot",
                schema: "korkut",
                table: "matters");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "korkut",
                table: "matters");

            migrationBuilder.DropColumn(
                name: "file_no",
                schema: "korkut",
                table: "matters");

            migrationBuilder.DropColumn(
                name: "opened_at",
                schema: "korkut",
                table: "matters");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "korkut",
                table: "matters");
        }
    }
}
