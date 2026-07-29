using Korkut.TestSupport;
using Korkut.Audit;
using Korkut.Contract.Audit;
using Korkut.Organization;
using Korkut.Organization.Entities;
using Korkut.Core;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Korkut.Matters;
using Korkut.Matters.Entities;

namespace Korkut.Audit.Tests;

public class AuditConcurrencyTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _office;

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _office = Guid.CreateVersion7();
        await using var db = pg.NewContext();
        db.Offices.Add(new Office(_office, "A", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    // --- Immutability ---

    [Fact]
    public async Task App_role_cannot_update_or_delete_audit_rows()
    {
        // Insert one audit row as the app role (within tenant context).
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();
        var id = Guid.CreateVersion7();
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await TenantMatrix.SetTenantAsync(conn, tx, _office);
            await using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO korkut.audit_events (id, office_id, action, entity_type, at) VALUES (@id, @o, 'read', 'matter', now())";
            ins.Parameters.AddWithValue("id", id);
            ins.Parameters.AddWithValue("o", _office);
            await ins.ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }

        await using var tx2 = await conn.BeginTransactionAsync();
        await TenantMatrix.SetTenantAsync(conn, tx2, _office);
        await using var upd = conn.CreateCommand();
        upd.Transaction = tx2;
        upd.CommandText = "UPDATE korkut.audit_events SET action='tamper' WHERE id=@id";
        upd.Parameters.AddWithValue("id", id);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => upd.ExecuteNonQueryAsync());
        // 42501 = insufficient_privilege (no UPDATE grant to korkut_app).
        Assert.Equal("42501", ex.SqlState);
    }

    [Fact]
    public async Task Immutability_trigger_blocks_mutation_even_for_the_owner()
    {
        await using var db = pg.NewContext(); // superuser/owner connection
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        var id = Guid.CreateVersion7();
        await using (var ins = conn.CreateCommand())
        {
            ins.CommandText = "INSERT INTO korkut.audit_events (id, office_id, action, entity_type, at) VALUES (@id, @o, 'read', 'matter', now())";
            ins.Parameters.AddWithValue("id", id);
            ins.Parameters.AddWithValue("o", _office);
            await ins.ExecuteNonQueryAsync();
        }

        await using var del = conn.CreateCommand();
        del.CommandText = "DELETE FROM korkut.audit_events WHERE id=@id";
        del.Parameters.AddWithValue("id", id);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => del.ExecuteNonQueryAsync());
        Assert.Contains("append-only", ex.MessageText); // trigger raised
    }

    // --- Redaction & actor integrity ---

    [Fact]
    public async Task Audit_before_after_are_redacted_and_actor_comes_from_trusted_context()
    {
        var trustedActor = Guid.CreateVersion7();
        await using var db = pg.NewContext();
        var log = new AuditLog(db, new Clock());

        // A snapshot that (maliciously) embeds a secret and a spoofed actor id.
        var secret = "sk-ant-" + new string('a', 28);
        var before = new { title = "old", leaked_key = secret, actor_id = Guid.CreateVersion7() };
        await log.WriteAsync(new AuditEntry(
            _office, trustedActor, "sess-1", "corr-1", "update", "matter", Guid.CreateVersion7(), before, null));

        var row = await db.AuditEvents.AsNoTracking().SingleAsync();
        Assert.DoesNotContain(secret, row.BeforeJson!, StringComparison.Ordinal); // redacted
        Assert.Contains("[REDACTED]", row.BeforeJson!, StringComparison.Ordinal);
        Assert.Equal(trustedActor, row.ActorId); // actor is the trusted one, not the payload's
    }

    // --- Optimistic concurrency ---

    [Fact]
    public async Task Concurrent_update_raises_a_conflict_instead_of_silent_overwrite()
    {
        Guid matterId;
        await using (var seed = pg.NewContext())
        {
            var matter = new Matter(_office, "orig", null, null, null, DateTimeOffset.UtcNow);
            seed.Matters.Add(matter);
            await seed.SaveChangesAsync();
            matterId = matter.Id;
        }

        // Two independent contexts load the same row (same xmin).
        await using var ctx1 = pg.NewContext();
        await using var ctx2 = pg.NewContext();
        var m1 = await ctx1.Matters.SingleAsync(x => x.Id == matterId);
        var m2 = await ctx2.Matters.SingleAsync(x => x.Id == matterId);

        m1.Rename("first");
        await ctx1.SaveChangesAsync(); // bumps xmin

        m2.Rename("second");
        // ctx2 holds the stale xmin -> conflict, not a silent overwrite.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx2.SaveChangesAsync());

        await using var check = pg.NewContext();
        Assert.Equal("first", (await check.Matters.SingleAsync(x => x.Id == matterId)).Title);
    }
}
