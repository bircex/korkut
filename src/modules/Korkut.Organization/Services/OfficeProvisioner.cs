using Korkut.Contract.Offices;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Organization.Services;

/// <summary>
/// Creates the install's office and its admin at installation, before any session/tenant context
/// exists, through the narrow BYPASSRLS SECURITY DEFINER function <c>korkut.setup_first_office</c>.
/// Returns null when nothing was created.
/// </summary>
public sealed class OfficeProvisioner(DbContext db, IClock clock) : IOfficeProvisioner
{
    public async Task<ProvisionedOffice?> SetupFirstOfficeAsync(string email, string passwordHash, string officeName, string fullName, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT office_id, user_id FROM korkut.setup_first_office(@e, @h, @n, @f, @now)";
        cmd.Parameters.AddWithValue("e", email);
        cmd.Parameters.AddWithValue("h", passwordHash);
        cmd.Parameters.AddWithValue("n", officeName);
        cmd.Parameters.AddWithValue("f", fullName);
        cmd.Parameters.AddWithValue("now", clock.UtcNow);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null; // already set up
        }

        return new ProvisionedOffice(reader.GetGuid(0), reader.GetGuid(1));
    }

    public async Task<InstallOffice?> InstallOfficeAsync(CancellationToken ct = default)
    {
        // The offices table is the tenant root and readable without a tenant scope.
        var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, disabled FROM korkut.offices ORDER BY created_at LIMIT 1";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new InstallOffice(reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2))
            : null;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        return conn;
    }
}
