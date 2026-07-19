using Npgsql;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// The authoritative list of tenant-owned (RLS-forced) tables. The coverage guard
/// asserts this equals what the database actually forces RLS on — so adding a new
/// tenant table without registering it (and thus without matrix coverage) fails
/// CI. Every entry declares how many rows each seeded office owns, so the matrix
/// can assert per-office visibility generically.
/// </summary>
public static class TenantMatrix
{
    // table -> (rows owned by office A, rows owned by office B) after seeding.
    public static readonly IReadOnlyDictionary<string, (int A, int B)> Tables =
        new Dictionary<string, (int, int)>
        {
            ["matters"] = (1, 2),
            ["matter_hearings"] = (1, 2),
            ["audit_events"] = (1, 2),
            ["users"] = (1, 2),
            ["password_reset_tokens"] = (1, 2),
            ["user_totp"] = (1, 2),
            ["recovery_codes"] = (1, 2),
            ["refresh_tokens"] = (1, 2),
            ["office_custom_roles"] = (1, 2),
            ["clients"] = (1, 2),
            ["courts"] = (1, 2),
            ["parties"] = (1, 2),
        };

    public static TheoryData<string> TableNames()
    {
        var data = new TheoryData<string>();
        foreach (var t in Tables.Keys)
        {
            data.Add(t);
        }

        return data;
    }

    public static async Task<long> CountAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT count(*) FROM korkut.{table}";
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task SetTenantAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid office)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT set_config('app.current_office', @o, true)";
        cmd.Parameters.AddWithValue("o", office.ToString());
        await cmd.ExecuteNonQueryAsync();
    }
}
