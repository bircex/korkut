using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfficeProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Self-serve signup provisions a brand-new tenant (a disabled office + its owner)
            // BEFORE any session/tenant context exists — inherently cross-tenant. Like the
            // other cross-RLS operations it goes through a narrow BYPASSRLS SECURITY DEFINER
            // function owned by korkut_resolver (the sanctioned pattern). The office starts
            // DISABLED (pending a platform-admin's approval); the owner is linked to the
            // Google subject. If the email already has an account, nothing is created.
            migrationBuilder.Sql(@"
                GRANT INSERT ON korkut.offices TO korkut_resolver;
                GRANT INSERT, SELECT ON korkut.users TO korkut_resolver;

                CREATE FUNCTION korkut.provision_office(
                    p_email text, p_google_sub text, p_office_name text, p_now timestamptz)
                RETURNS TABLE(office_id uuid, user_id uuid)
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = korkut AS $fn$
                DECLARE
                    v_office uuid;
                    v_user uuid;
                BEGIN
                    -- Never duplicate an existing identity (by email or Google subject).
                    IF EXISTS (SELECT 1 FROM korkut.users
                               WHERE lower(email) = lower(p_email) OR google_sub = p_google_sub) THEN
                        RETURN;
                    END IF;

                    v_office := gen_random_uuid();
                    v_user := gen_random_uuid();

                    INSERT INTO korkut.offices (id, name, disabled, require_two_factor, created_at)
                        VALUES (v_office, p_office_name, true, false, p_now);
                    INSERT INTO korkut.users
                        (id, office_id, email, role, google_sub, two_factor_enabled,
                         security_stamp, token_epoch, failed_access_count, created_at)
                        VALUES (v_user, v_office, lower(p_email), 'owner', p_google_sub, false,
                                gen_random_uuid(), 0, 0, p_now);

                    RETURN QUERY SELECT v_office, v_user;
                END;
                $fn$;
                ALTER FUNCTION korkut.provision_office(text, text, text, timestamptz) OWNER TO korkut_resolver;
                REVOKE ALL ON FUNCTION korkut.provision_office(text, text, text, timestamptz) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION korkut.provision_office(text, text, text, timestamptz) TO korkut_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS korkut.provision_office(text, text, text, timestamptz);
                REVOKE INSERT ON korkut.offices FROM korkut_resolver;
                REVOKE INSERT, SELECT ON korkut.users FROM korkut_resolver;");
        }
    }
}
