using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Infrastructure.Persistence;

/// <summary>The office/user a reset token belongs to (resolved across tenants).</summary>
public sealed record ResetTokenOwner(Guid OfficeId, Guid UserId);

/// <summary>
/// Resolves the tenant of an opaque password-reset token before any session exists,
/// via the narrow BYPASSRLS SECURITY DEFINER function <c>korkut.resolve_reset_token</c>
/// (the only sanctioned RLS bypass for this flow). Returns null for an unknown,
/// expired or already-used token.
/// </summary>
public sealed class EfResetTokenResolver(KorkutDbContext db, IClock clock)
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
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new ResetTokenOwner(reader.GetGuid(0), reader.GetGuid(1));
    }
}
