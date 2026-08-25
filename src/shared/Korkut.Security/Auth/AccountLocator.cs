using Korkut.Organization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Security.Auth;

/// <summary>
/// Finds accounts by e-mail across offices through the narrow BYPASSRLS SECURITY DEFINER
/// function <c>korkut.resolve_users_by_email</c>. Sign-in cannot be tenant-scoped until the
/// office is known, so this one lookup is the sanctioned exception — owned by a dedicated
/// role, never the app role itself.
/// </summary>
public sealed class AccountLocator(DbContext db) : IAccountLocator
{
    public Task<IReadOnlyList<AccountLocation>> FindByEmailAsync(
        string normalizedEmail, CancellationToken ct = default)
        => ResolveAsync("korkut.resolve_users_by_email", normalizedEmail, ct);

    public Task<IReadOnlyList<AccountLocation>> FindAnyByEmailAsync(
        string normalizedEmail, CancellationToken ct = default)
        => ResolveAsync("korkut.resolve_all_users_by_email", normalizedEmail, ct);

    private async Task<IReadOnlyList<AccountLocation>> ResolveAsync(
        string function, string normalizedEmail, CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        // function is a fixed internal constant (never caller input); the e-mail is parameterized.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {function}(@p)";
        cmd.Parameters.AddWithValue("p", normalizedEmail);
        var accounts = new List<AccountLocation>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            accounts.Add(new AccountLocation(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2)));
        }

        return accounts;
    }

    public async Task<bool> IsOfficeDisabledAsync(Guid officeId, CancellationToken ct = default)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT korkut.is_office_disabled(@o)";
        cmd.Parameters.AddWithValue("o", officeId);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }
}
