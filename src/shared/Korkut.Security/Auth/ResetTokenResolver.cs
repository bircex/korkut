using Korkut.Organization;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Security.Auth;

/// <summary>
/// Resolves the tenant of an opaque password-reset token before any session exists, via
/// the narrow BYPASSRLS SECURITY DEFINER function <c>korkut.resolve_reset_token</c> (the
/// only sanctioned RLS bypass for this flow). Null for an unknown, expired or already-used
/// token.
/// </summary>
public sealed class ResetTokenResolver(DbContext db, IClock clock) : IResetTokenResolver
{
    public async Task<ResetTokenOwner?> ResolveAsync(string tokenHash, CancellationToken ct = default)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT office_id, user_id FROM korkut.resolve_reset_token(@h, @now)";
        cmd.Parameters.AddWithValue("h", tokenHash);
        cmd.Parameters.AddWithValue("now", clock.UtcNow);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new ResetTokenOwner(reader.GetGuid(0), reader.GetGuid(1))
            : null;
    }
}
