using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFirstRunSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First-run setup on a fresh install: the office and its owner are created from the
            // browser, before any session exists. Both functions follow the sanctioned RLS-bypass
            // pattern of the other resolvers (SECURITY DEFINER, owned by korkut_resolver, EXECUTE
            // granted only to korkut_app), and both answer about the whole install, which the app
            // role cannot see through RLS.
            //
            // setup_first_office creates the office ENABLED (unlike provision_office_local, which
            // leaves it for an approval) and does so only while no office exists. A transaction-
            // scoped advisory lock serialises concurrent setups, so two browsers racing on a fresh
            // install produce exactly one office; the loser gets no row back.
            migrationBuilder.Sql(@"
                GRANT USAGE, CREATE ON SCHEMA korkut TO korkut_resolver;

                CREATE FUNCTION korkut.is_setup_complete()
                RETURNS boolean
                LANGUAGE sql STABLE SECURITY DEFINER SET search_path = korkut AS $fn$
                    SELECT EXISTS (SELECT 1 FROM korkut.offices);
                $fn$;
                ALTER FUNCTION korkut.is_setup_complete() OWNER TO korkut_resolver;
                REVOKE ALL ON FUNCTION korkut.is_setup_complete() FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION korkut.is_setup_complete() TO korkut_app;

                CREATE FUNCTION korkut.setup_first_office(
                    p_email text, p_password_hash text, p_office_name text, p_full_name text, p_now timestamptz)
                RETURNS TABLE(office_id uuid, user_id uuid)
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = korkut AS $fn$
                DECLARE
                    v_office uuid;
                    v_user uuid;
                BEGIN
                    PERFORM pg_advisory_xact_lock(hashtext('korkut.setup_first_office'));
                    IF EXISTS (SELECT 1 FROM korkut.offices) THEN
                        RETURN;
                    END IF;

                    v_office := gen_random_uuid();
                    v_user := gen_random_uuid();

                    INSERT INTO korkut.offices (id, name, disabled, require_two_factor, created_at)
                        VALUES (v_office, p_office_name, false, false, p_now);
                    INSERT INTO korkut.users
                        (id, office_id, email, full_name, role, password_hash, two_factor_enabled,
                         security_stamp, token_epoch, failed_access_count, created_at)
                        VALUES (v_user, v_office, lower(p_email), p_full_name, 'owner', p_password_hash, false,
                                gen_random_uuid(), 0, 0, p_now);

                    RETURN QUERY SELECT v_office, v_user;
                END;
                $fn$;
                ALTER FUNCTION korkut.setup_first_office(text, text, text, text, timestamptz) OWNER TO korkut_resolver;
                REVOKE ALL ON FUNCTION korkut.setup_first_office(text, text, text, text, timestamptz) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION korkut.setup_first_office(text, text, text, text, timestamptz) TO korkut_app;

                REVOKE CREATE ON SCHEMA korkut FROM korkut_resolver;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS korkut.setup_first_office(text, text, text, text, timestamptz);
                DROP FUNCTION IF EXISTS korkut.is_setup_complete();");
        }
    }
}
