using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sources_json",
                schema: "korkut",
                table: "chat_messages",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sources_json",
                schema: "korkut",
                table: "chat_messages");
        }
    }
}
