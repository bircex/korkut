using Korkut.Security;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Security;

public enum RotateStatus
{
    Rotated,
    ReuseRevoked,
    Invalid,
    Expired,
}

public sealed record RefreshIssue(string RawToken, Guid FamilyId);

public sealed record RotateOutcome(RotateStatus Status, string? NewRawToken, Guid OfficeId, Guid UserId, Guid FamilyId);

/// <summary>
/// Opaque, hashed, family-grouped refresh tokens with one-time rotation and
/// reuse detection. Rotation is a single atomic <c>SECURITY DEFINER</c> function
/// (FOR UPDATE) so a replayed token can never race past the reuse check; a reuse
/// revokes the whole family. Tokens are pre-session (office unknown until lookup),
/// so — like sign-in — they resolve via the BYPASSRLS resolver role, not the app
/// role's own RLS bypass.
/// </summary>
public sealed class RefreshTokenService(DbContext db, ITokenHasher tokenHasher, IClock clock)
{
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(14);

    public async Task<RefreshIssue> IssueAsync(Guid officeId, Guid userId, CancellationToken ct = default)
    {
        var raw = tokenHasher.NewRawToken();
        var family = Guid.CreateVersion7();
        var now = clock.UtcNow;
        await ExecAsync("SELECT korkut.issue_refresh_token(@id, @fam, @off, @usr, @hash, @exp, @now)", ct,
            ("id", UuidV7.NewId()), ("fam", family), ("off", officeId), ("usr", userId),
            ("hash", tokenHasher.Hash(raw)), ("exp", now + Lifetime), ("now", now));
        return new RefreshIssue(raw, family);
    }

    public async Task<RotateOutcome> RotateAsync(string rawToken, CancellationToken ct = default)
    {
        var newRaw = tokenHasher.NewRawToken();
        var now = clock.UtcNow;
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(conn, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM korkut.rotate_refresh_token(@hash, @newid, @newhash, @exp, @now)";
        cmd.Parameters.AddWithValue("hash", tokenHasher.Hash(rawToken));
        cmd.Parameters.AddWithValue("newid", UuidV7.NewId());
        cmd.Parameters.AddWithValue("newhash", tokenHasher.Hash(newRaw));
        cmd.Parameters.AddWithValue("exp", now + Lifetime);
        cmd.Parameters.AddWithValue("now", now);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new RotateOutcome(RotateStatus.Invalid, null, Guid.Empty, Guid.Empty, Guid.Empty);
        }

        var status = reader.GetString(0);
        var office = reader.IsDBNull(1) ? Guid.Empty : reader.GetGuid(1);
        var user = reader.IsDBNull(2) ? Guid.Empty : reader.GetGuid(2);
        var family = reader.IsDBNull(3) ? Guid.Empty : reader.GetGuid(3);
        return status switch
        {
            "rotated" => new RotateOutcome(RotateStatus.Rotated, newRaw, office, user, family),
            "reuse_revoked" => new RotateOutcome(RotateStatus.ReuseRevoked, null, office, user, family),
            "expired" => new RotateOutcome(RotateStatus.Expired, null, office, user, family),
            _ => new RotateOutcome(RotateStatus.Invalid, null, office, user, family),
        };
    }

    public Task RevokeFamilyAsync(Guid familyId, CancellationToken ct = default)
        => ExecAsync("SELECT korkut.revoke_refresh_family(@fam, @now)", ct, ("fam", familyId), ("now", clock.UtcNow));

    private async Task ExecAsync(string sql, CancellationToken ct, params (string, object)[] ps)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(conn, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in ps)
        {
            cmd.Parameters.AddWithValue(k, v);
        }

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureOpenAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }
    }
}
