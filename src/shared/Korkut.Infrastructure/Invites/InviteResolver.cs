using Korkut.Organization;
using Korkut.Core;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Infrastructure.Invites;

/// <summary>
/// Resolves an opaque invite token before any session exists, through the narrow BYPASSRLS
/// SECURITY DEFINER function <c>korkut.resolve_invite_token</c> — the app role itself never
/// gets to read across tenants. Returns null for an unknown, expired or already-accepted
/// invite, so the three are indistinguishable to a caller.
/// </summary>
public sealed class InviteResolver(KorkutDbContext db, IClock clock) : IInviteResolver
{
    public async Task<InviteGrant?> ResolveAsync(string tokenHash, CancellationToken ct = default)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, office_id, email, role FROM korkut.resolve_invite_token(@h, @now)";
        cmd.Parameters.AddWithValue("h", tokenHash);
        cmd.Parameters.AddWithValue("now", clock.UtcNow);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new InviteGrant(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3));
    }
}
