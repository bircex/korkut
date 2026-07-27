using Korkut.Application.Auth;
using Korkut.Organization;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Infrastructure.Auth;

/// <summary>
/// Finds accounts by e-mail across offices through the narrow BYPASSRLS SECURITY DEFINER
/// function <c>korkut.resolve_users_by_email</c>. Sign-in cannot be tenant-scoped until the
/// office is known, so this one lookup is the sanctioned exception — owned by a dedicated
/// role, never the app role itself.
/// </summary>
public sealed class AccountLocator(KorkutDbContext db) : IAccountLocator
{
    public async Task<IReadOnlyList<AccountLocation>> FindByEmailAsync(
        string normalizedEmail, CancellationToken ct = default)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM korkut.resolve_users_by_email(@p)";
        cmd.Parameters.AddWithValue("p", normalizedEmail);
        var accounts = new List<AccountLocation>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            accounts.Add(new AccountLocation(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2)));
        }

        return accounts;
    }
}
