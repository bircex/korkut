using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreOfficeRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_office_custom_roles",
                schema: "korkut",
                table: "office_custom_roles");

            migrationBuilder.RenameTable(
                name: "office_custom_roles",
                schema: "korkut",
                newName: "office_roles",
                newSchema: "korkut");

            migrationBuilder.RenameIndex(
                name: "ux_custom_roles_office_key",
                schema: "korkut",
                table: "office_roles",
                newName: "ux_office_roles_office_key");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "korkut",
                table: "invites",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "korkut",
                table: "office_roles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "korkut",
                table: "office_roles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                schema: "korkut",
                table: "office_roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "korkut",
                table: "office_roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddPrimaryKey(
                name: "PK_office_roles",
                schema: "korkut",
                table: "office_roles",
                column: "id");
            migrationBuilder.Sql("UPDATE korkut.office_roles SET updated_at = created_at;");

            // The table keeps its grants and its tenant_isolation policy through the rename; the
            // system roles themselves are created by the app on start (RoleService).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_office_roles",
                schema: "korkut",
                table: "office_roles");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "korkut",
                table: "office_roles");

            migrationBuilder.DropColumn(
                name: "is_system",
                schema: "korkut",
                table: "office_roles");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "korkut",
                table: "office_roles");

            migrationBuilder.RenameTable(
                name: "office_roles",
                schema: "korkut",
                newName: "office_custom_roles",
                newSchema: "korkut");

            migrationBuilder.RenameIndex(
                name: "ux_office_roles_office_key",
                schema: "korkut",
                table: "office_custom_roles",
                newName: "ux_custom_roles_office_key");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "korkut",
                table: "invites",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "korkut",
                table: "office_custom_roles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddPrimaryKey(
                name: "PK_office_custom_roles",
                schema: "korkut",
                table: "office_custom_roles",
                column: "id");
        }
    }
}
