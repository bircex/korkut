using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JobTableAppRoleGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The job tables (outbox/inbox/job_audit) were created before the least-
            // privileged app role existed (KDM-024), so korkut_app was never granted
            // access. The app legitimately enqueues transactional-outbox events (e.g.
            // the document de-index event on tombstone), so grant it the needed DML.
            // Rows carry office_id and the app always writes its own tenant's id; the
            // worker reads across tenants under its own identity, so the outbox is not
            // RLS-forced here.
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE ON korkut.outbox_messages TO korkut_app;
                GRANT SELECT, INSERT, UPDATE ON korkut.inbox_entries TO korkut_app;
                GRANT SELECT, INSERT ON korkut.job_audit TO korkut_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                REVOKE SELECT, INSERT, UPDATE ON korkut.outbox_messages FROM korkut_app;
                REVOKE SELECT, INSERT, UPDATE ON korkut.inbox_entries FROM korkut_app;
                REVOKE SELECT, INSERT ON korkut.job_audit FROM korkut_app;");
        }
    }
}
