using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGoogleSignIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sign-in is local only now. The e-mail resolver loses its "not Google-linked" filter
            // (replaced first: its body reads the column dropped below), and the Google resolvers
            // go, along with the SSO-inclusive twin the password reset used.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION korkut.resolve_users_by_email(p_email text)
                RETURNS TABLE(id uuid, office_id uuid, email text)
                LANGUAGE sql SECURITY DEFINER SET search_path = korkut AS $fn$
                    SELECT id, office_id, email FROM korkut.users WHERE email = p_email;
                $fn$;

                DROP FUNCTION IF EXISTS korkut.resolve_user_by_google_sub(text);
                DROP FUNCTION IF EXISTS korkut.link_google_sub(uuid, text);
                DROP FUNCTION IF EXISTS korkut.resolve_all_users_by_email(text);");

            migrationBuilder.DropIndex(
                name: "ux_users_google_sub",
                schema: "korkut",
                table: "users");

            migrationBuilder.DropColumn(
                name: "google_sub",
                schema: "korkut",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The Google resolvers are not recreated: rolling back restores the column only.
            migrationBuilder.AddColumn<string>(
                name: "google_sub",
                schema: "korkut",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_google_sub",
                schema: "korkut",
                table: "users",
                column: "google_sub",
                unique: true);
        }
    }
}
