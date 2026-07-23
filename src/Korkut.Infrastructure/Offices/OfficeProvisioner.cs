using Korkut.Application.Offices;
using Korkut.Domain.Offices;
using Korkut.Domain.Time;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Infrastructure.Offices;

/// <summary>
/// Provisions a brand-new tenant (a disabled office + its owner) for self-serve signup,
/// before any session/tenant context exists, via the narrow BYPASSRLS SECURITY DEFINER
/// function <c>korkut.provision_office</c>. Returns null if the email/Google subject
/// already has an account (nothing is created).
/// </summary>
public sealed class OfficeProvisioner(KorkutDbContext db, IClock clock) : IOfficeProvisioner
{
    public async Task<ProvisionedOffice?> ProvisionAsync(string email, string googleSub, string officeName, CancellationToken ct = default)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

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
}
