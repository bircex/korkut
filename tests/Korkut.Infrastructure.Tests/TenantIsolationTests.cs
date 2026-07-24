using Korkut.Domain.Offices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Korkut.Domain.Matters;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// RLS tenant-isolation negatives on real PostgreSQL, run AS the least-privileged
/// app role (superusers bypass RLS, so these must not use the superuser).
/// </summary>
public class TenantIsolationTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _officeA;
    private Guid _officeB;
    private Guid _matterA; // a matter owned by office A

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _officeA = Guid.CreateVersion7();
        _officeB = Guid.CreateVersion7();

        // Seed cross-tenant data via the superuser context (RLS bypassed): A has 1
        // matter, B has 2, so counts distinguish which office a reader sees.
        await using var db = pg.NewContext();
        db.Offices.Add(new Office(_officeA, "A", DateTimeOffset.UtcNow));
        db.Offices.Add(new Office(_officeB, "B", DateTimeOffset.UtcNow));
        var matterA = new Matter(_officeA, "A-1", null, null, null, DateTimeOffset.UtcNow);
        _matterA = matterA.Id;
        db.Matters.Add(matterA);
        db.Matters.Add(new Matter(_officeB, "B-1", null, null, null, DateTimeOffset.UtcNow));
        db.Matters.Add(new Matter(_officeB, "B-2", null, null, null, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task SetTenant(NpgsqlConnection conn, NpgsqlTransaction tx, Guid office)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT set_config('app.current_office', @o, true)";
        cmd.Parameters.AddWithValue("o", office.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> Scalar(NpgsqlConnection conn, NpgsqlTransaction tx, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task App_role_is_not_superuser_or_bypassrls()
    {
        await using var db = pg.NewContext();
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT rolsuper, rolbypassrls FROM pg_roles WHERE rolname='korkut_app'";
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.False(reader.GetBoolean(0), "korkut_app must not be superuser");
        Assert.False(reader.GetBoolean(1), "korkut_app must not have BYPASSRLS");
    }

    [Fact]
    public async Task Without_tenant_context_no_rows_are_visible()
    {
        await using var conn = new NpgsqlConnection(pg.AppConnectionStringFresh);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        // No set_config -> current_setting(..., true) is NULL -> no rows match.
        Assert.Equal(0, await Scalar(conn, tx, "SELECT count(*) FROM korkut.matters"));
    }

    [Fact]
    public async Task With_tenant_context_only_own_office_rows_are_visible()
    {
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();
        await using (var txA = await conn.BeginTransactionAsync())
        {
            await SetTenant(conn, txA, _officeA);
            Assert.Equal(1, await Scalar(conn, txA, "SELECT count(*) FROM korkut.matters"));
        }

        await using var txB = await conn.BeginTransactionAsync();
        await SetTenant(conn, txB, _officeB);
        Assert.Equal(2, await Scalar(conn, txB, "SELECT count(*) FROM korkut.matters"));
    }

    [Fact]
    public async Task Insert_into_another_office_is_rejected_by_with_check()
    {
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await SetTenant(conn, tx, _officeA);

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO korkut.matters (id, office_id, title, created_at) VALUES (@id, @off, 'x', now())";
        cmd.Parameters.AddWithValue("id", Guid.CreateVersion7());
        cmd.Parameters.AddWithValue("off", _officeB); // wrong office while scoped to A
        await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Update_and_delete_do_not_touch_other_office_rows()
    {
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await SetTenant(conn, tx, _officeB); // scoped to B; A's matter is invisible

        await using var upd = conn.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = "UPDATE korkut.matters SET title='hacked' WHERE id=@id";
        upd.Parameters.AddWithValue("id", _matterA);
        Assert.Equal(0, await upd.ExecuteNonQueryAsync()); // A's row not visible/affected

        await using var del = conn.CreateCommand();
        del.Transaction = tx;
        del.CommandText = "DELETE FROM korkut.matters WHERE id=@id";
        del.Parameters.AddWithValue("id", _matterA);
        Assert.Equal(0, await del.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Cross_tenant_composite_fk_is_rejected()
    {
        // Superuser (RLS bypassed) so we isolate the FK behavior: a hearing in office B
        // referencing office A's matter has no matching (office_id, id) parent.
        await using var db = pg.NewContext();
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO korkut.matter_hearings (id, office_id, matter_id, scheduled_at) VALUES (@id, @off, @m, now())";
        cmd.Parameters.AddWithValue("id", Guid.CreateVersion7());
        cmd.Parameters.AddWithValue("off", _officeB);   // office B
        cmd.Parameters.AddWithValue("m", _matterA);     // but matter belongs to A
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("23503", ex.SqlState); // foreign_key_violation
    }

    [Fact]
    public async Task Tenant_context_does_not_leak_across_pooled_connection_reuse()
    {
        // MaxPoolSize=1 forces the same physical connection to be reused.
        long seenAsA, seenAsB;
        await using (var conn = new NpgsqlConnection(pg.AppConnectionString))
        {
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();
            await SetTenant(conn, tx, _officeA);
            seenAsA = await Scalar(conn, tx, "SELECT count(*) FROM korkut.matters");
        }

        // The physical connection returns to the pool; a fresh logical connection
        // reuses it. If the tenant setting leaked, we'd still see office A's rows.
        await using (var conn = new NpgsqlConnection(pg.AppConnectionString))
        {
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();
            await SetTenant(conn, tx, _officeB);
            seenAsB = await Scalar(conn, tx, "SELECT count(*) FROM korkut.matters");
        }

        Assert.Equal(1, seenAsA);
        Assert.Equal(2, seenAsB); // sees only B — no leak of A's context
    }
}
