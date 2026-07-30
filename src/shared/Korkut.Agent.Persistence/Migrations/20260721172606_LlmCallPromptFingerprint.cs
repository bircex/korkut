using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Agent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LlmCallPromptFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "prompt_fingerprint",
                schema: "korkut_agent",
                table: "llm_calls",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "prompt_fingerprint",
                schema: "korkut_agent",
                table: "llm_calls");
        }
    }
}
