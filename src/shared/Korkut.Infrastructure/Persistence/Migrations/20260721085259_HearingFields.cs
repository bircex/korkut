using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HearingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "location",
                schema: "korkut",
                table: "matter_hearings",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                schema: "korkut",
                table: "matter_hearings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reminder_status",
                schema: "korkut",
                table: "matter_hearings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "result",
                schema: "korkut",
                table: "matter_hearings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "korkut",
                table: "matter_hearings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "location",
                schema: "korkut",
                table: "matter_hearings");

            migrationBuilder.DropColumn(
                name: "notes",
                schema: "korkut",
                table: "matter_hearings");

            migrationBuilder.DropColumn(
                name: "reminder_status",
                schema: "korkut",
                table: "matter_hearings");

            migrationBuilder.DropColumn(
                name: "result",
                schema: "korkut",
                table: "matter_hearings");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "korkut",
                table: "matter_hearings");
        }
    }
}
