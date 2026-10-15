using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Matter scope in the database itself (docs/MATTER_RBAC_PLAN.md §5), under the app's own checks:
    /// a member's transaction carries app.current_user and app.all_matters, and the matter-bound
    /// tables show only rows of matters the member is on (or every matter with matters.view_all).
    /// A scope with no member behind it (a background job, a request before sign-in) keeps the
    /// office boundary only, as before. Rows with no matter are left to the app's ownership rules.
    /// </summary>
    public partial class ScopeMatterRows : Migration
    {
        // Tables whose rows belong to a matter through matter_id (finance stays out: it is gated by
        // permission and accounting does not see every matter; documents link through relations).
        private static readonly string[] MatterBound =
        [
            "matter_hearings", "matter_members", "parties", "conversations", "correspondence",
            "deadlines", "communication_notes", "tasks",
        ];

        private const string Office = "office_id = current_setting('app.current_office', true)::uuid";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The visibility check reads matter_members past its own policy, so it is a SECURITY
            // DEFINER function owned by the BYPASSRLS resolver role (the sanctioned pattern).
            migrationBuilder.Sql(@"
                GRANT korkut_resolver TO CURRENT_USER;
                GRANT USAGE, CREATE ON SCHEMA korkut TO korkut_resolver;
                GRANT SELECT ON korkut.matter_members TO korkut_resolver;

                CREATE FUNCTION korkut.can_see_matter(p_matter uuid)
                RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path = korkut AS $fn$
                    SELECT COALESCE(current_setting('app.current_user', true), '') = ''
                        OR current_setting('app.all_matters', true) = '1'
                        OR EXISTS (
                            SELECT 1 FROM korkut.matter_members mm
                            WHERE mm.matter_id = p_matter
                              AND mm.office_id = NULLIF(current_setting('app.current_office', true), '')::uuid
                              AND mm.user_id = NULLIF(current_setting('app.current_user', true), '')::uuid);
                $fn$;

                -- (SQL does not promise to stop at the first true OR, hence NULLIF before each cast.)
                -- A matter with no team yet: true for the moment between a new matter's insert and
                -- its lead's (an insert returning the row must be able to read it back).
                CREATE FUNCTION korkut.matter_has_team(p_matter uuid)
                RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path = korkut AS $fn$
                    SELECT EXISTS (SELECT 1 FROM korkut.matter_members mm WHERE mm.matter_id = p_matter);
                $fn$;

                ALTER FUNCTION korkut.can_see_matter(uuid) OWNER TO korkut_resolver;
                ALTER FUNCTION korkut.matter_has_team(uuid) OWNER TO korkut_resolver;
                REVOKE ALL ON FUNCTION korkut.can_see_matter(uuid) FROM PUBLIC;
                REVOKE ALL ON FUNCTION korkut.matter_has_team(uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION korkut.can_see_matter(uuid) TO korkut_app;
                GRANT EXECUTE ON FUNCTION korkut.matter_has_team(uuid) TO korkut_app;
                REVOKE CREATE ON SCHEMA korkut FROM korkut_resolver;");

            // Reading, changing and deleting need the matter in scope; writing a row needs only the
            // office (the app checks the matter before it writes, and a new matter has no team yet).
            migrationBuilder.Sql($@"
                DROP POLICY tenant_isolation ON korkut.matters;
                CREATE POLICY tenant_isolation ON korkut.matters
                    USING ({Office} AND (korkut.can_see_matter(id) OR NOT korkut.matter_has_team(id)))
                    WITH CHECK ({Office});");
            foreach (var table in MatterBound)
            {
                migrationBuilder.Sql($@"
                    DROP POLICY tenant_isolation ON korkut.{table};
                    CREATE POLICY tenant_isolation ON korkut.{table}
                        USING ({Office} AND (matter_id IS NULL OR korkut.can_see_matter(matter_id)))
                        WITH CHECK ({Office});");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in MatterBound.Prepend("matters"))
            {
                migrationBuilder.Sql($@"
                    DROP POLICY tenant_isolation ON korkut.{table};
                    CREATE POLICY tenant_isolation ON korkut.{table} USING ({Office}) WITH CHECK ({Office});");
            }

            migrationBuilder.Sql(@"
                DROP FUNCTION korkut.can_see_matter(uuid);
                DROP FUNCTION korkut.matter_has_team(uuid);");
        }
    }
}
