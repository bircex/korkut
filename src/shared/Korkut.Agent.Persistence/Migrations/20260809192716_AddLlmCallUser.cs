using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Agent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmCallUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                schema: "korkut_agent",
                table: "llm_calls",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_llm_calls_office_user_created",
                schema: "korkut_agent",
                table: "llm_calls",
                columns: new[] { "office_id", "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_llm_calls_office_user_created",
                schema: "korkut_agent",
                table: "llm_calls");

            migrationBuilder.DropColumn(
                name: "user_id",
                schema: "korkut_agent",
                table: "llm_calls");
        }
    }
}
