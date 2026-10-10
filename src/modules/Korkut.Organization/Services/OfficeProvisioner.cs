using Korkut.Contract.Offices;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Organization.Services;

/// <summary>
/// Creates offices before any session/tenant context exists, through the narrow BYPASSRLS
/// SECURITY DEFINER functions: the install's first office at first-run setup
/// (<c>korkut.setup_first_office</c>), and the self-serve signup's disabled office + owner
/// (<c>korkut.provision_office</c>). Returns null when nothing was created.
/// </summary>
public sealed class OfficeProvisioner(DbContext db, IClock clock) : IOfficeProvisioner
{
    public async Task<ProvisionedOffice?> ProvisionAsync(string email, string googleSub, string officeName, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT office_id, user_id FROM korkut.provision_office(@e, @g, @n, @now)";
        cmd.Parameters.AddWithValue("e", email);
        cmd.Parameters.AddWithValue("g", googleSub);
        cmd.Parameters.AddWithValue("n", officeName);
        cmd.Parameters.AddWithValue("now", clock.UtcNow);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null; // email/subject already registered
        }

        return new ProvisionedOffice(reader.GetGuid(0), reader.GetGuid(1));
    }

    public async Task<ProvisionedOffice?> ProvisionLocalAsync(string email, string passwordHash, string officeName, string fullName, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT office_id, user_id FROM korkut.provision_office_local(@e, @h, @n, @f, @now)";
        cmd.Parameters.AddWithValue("e", email);
        cmd.Parameters.AddWithValue("h", passwordHash);
        cmd.Parameters.AddWithValue("n", officeName);
        cmd.Parameters.AddWithValue("f", fullName);
        cmd.Parameters.AddWithValue("now", clock.UtcNow);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null; // email already registered
        }

        return new ProvisionedOffice(reader.GetGuid(0), reader.GetGuid(1));
    }

    public async Task<bool> IsSetupCompleteAsync(CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT korkut.is_setup_complete()";
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

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
