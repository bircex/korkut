using Korkut.Application.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// Google sign-in resolution over PostgreSQL. Sign-in is inherently cross-tenant
/// (the office is unknown until the identity resolves), so these lookups go through
/// narrow <c>SECURITY DEFINER</c> resolver functions owned by a dedicated
/// BYPASSRLS role — the explicit, auditable exception to RLS (never the app role
/// itself). See migration GoogleOidc.
/// </summary>
public sealed class EfGoogleUserDirectory(KorkutDbContext db) : IGoogleUserDirectory
{
    public async Task<AuthUser?> FindByGoogleSubAsync(string googleSub, CancellationToken ct)
    {
        var rows = await QueryUsersAsync("SELECT * FROM korkut.resolve_user_by_google_sub(@p)", ("p", googleSub), ct);
        return rows.Count > 0 ? rows[0] : null;
    }

    public async Task<IReadOnlyList<AuthUser>> FindByEmailAcrossOfficesAsync(string normalizedEmail, CancellationToken ct)
        => await QueryUsersAsync("SELECT * FROM korkut.resolve_users_by_email(@p)", ("p", normalizedEmail), ct);

    public Task LinkGoogleSubAsync(Guid userId, string googleSub, CancellationToken ct)
        => ExecAsync("SELECT korkut.link_google_sub(@u, @s)", ct, ("u", userId), ("s", googleSub));

    public Task<bool> IsOfficeDisabledAsync(Guid officeId, CancellationToken ct)
        => ScalarBoolAsync("SELECT korkut.is_office_disabled(@o)", ct, ("o", officeId));

    public Task<bool> OfficeRequiresTwoFactorAsync(Guid officeId, CancellationToken ct)
        => ScalarBoolAsync("SELECT korkut.office_requires_two_factor(@o)", ct, ("o", officeId));

    public Task<bool> UserHasTwoFactorAsync(Guid userId, CancellationToken ct)
        => ScalarBoolAsync("SELECT korkut.user_has_two_factor(@u)", ct, ("u", userId));

    private async Task<IReadOnlyList<AuthUser>> QueryUsersAsync(string sql, (string, object) param, CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(conn, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue(param.Item1, param.Item2);
        var users = new List<AuthUser>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            users.Add(new AuthUser(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
                null, 0, null, Guid.Empty, 0));
        }

        return users;
    }

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

    private async Task<bool> ScalarBoolAsync(string sql, CancellationToken ct, params (string, object)[] ps)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(conn, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in ps)
        {
            cmd.Parameters.AddWithValue(k, v);
        }

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    private static async Task EnsureOpenAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }
    }
}
