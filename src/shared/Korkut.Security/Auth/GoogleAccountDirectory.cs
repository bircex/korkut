using Korkut.Organization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Security.Auth;

/// <summary>
/// Google account resolution over PostgreSQL. Sign-in is inherently cross-tenant (the
/// office is unknown until the identity resolves), so these lookups go through narrow
/// <c>SECURITY DEFINER</c> resolver functions owned by a dedicated BYPASSRLS role — the
/// explicit, auditable exception to RLS, never the app role itself. See migration
/// GoogleOidc.
/// </summary>
public sealed class GoogleAccountDirectory(DbContext db) : IGoogleAccountDirectory
{
    public async Task<AccountLocation?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default)
    {
        var rows = await QueryAccountsAsync("SELECT * FROM korkut.resolve_user_by_google_sub(@p)", ("p", googleSub), ct);
        return rows.Count > 0 ? rows[0] : null;
    }

    public Task LinkGoogleSubAsync(Guid userId, string googleSub, CancellationToken ct = default)
        => ExecAsync("SELECT korkut.link_google_sub(@u, @s)", ct, ("u", userId), ("s", googleSub));

    public Task<bool> IsOfficeDisabledAsync(Guid officeId, CancellationToken ct = default)
        => ScalarBoolAsync("SELECT korkut.is_office_disabled(@o)", ct, ("o", officeId));

    public Task<bool> OfficeRequiresTwoFactorAsync(Guid officeId, CancellationToken ct = default)
        => ScalarBoolAsync("SELECT korkut.office_requires_two_factor(@o)", ct, ("o", officeId));

    public Task<bool> UserHasTwoFactorAsync(Guid userId, CancellationToken ct = default)
        => ScalarBoolAsync("SELECT korkut.user_has_two_factor(@u)", ct, ("u", userId));

    private async Task<IReadOnlyList<AccountLocation>> QueryAccountsAsync(string sql, (string, object) param, CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(conn, ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue(param.Item1, param.Item2);
        var accounts = new List<AccountLocation>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            accounts.Add(new AccountLocation(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2)));
        }

        return accounts;
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
