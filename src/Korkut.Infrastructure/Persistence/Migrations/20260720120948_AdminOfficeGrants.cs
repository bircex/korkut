using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korkut.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminOfficeGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Platform admins toggle office enable/disable (and the 2FA requirement)
            // cross-tenant. offices is not RLS-forced (it's the tenant root), so the
            // app role already has SELECT; grant column-scoped UPDATE for exactly those
            // two fields. Only the admin-audience endpoints ever write them.
            migrationBuilder.Sql("GRANT UPDATE (disabled, require_two_factor) ON korkut.offices TO korkut_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("REVOKE UPDATE (disabled, require_two_factor) ON korkut.offices FROM korkut_app;");
        }
    }
}
