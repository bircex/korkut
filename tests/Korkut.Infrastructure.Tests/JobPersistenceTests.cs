using Korkut.TestSupport;
using Korkut.Infrastructure.Jobs;
using Korkut.Jobs.Entities;
using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;
using Korkut.Infrastructure.Persistence;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Tests;

public class JobPersistenceTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    // Reset tables before each test so the shared container gives isolated state.
    public Task InitializeAsync() => pg.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class CompletingHandler(string type) : IJobHandler
    {
        public int Invocations { get; private set; }

        public string MessageType { get; } = type;

        public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            Invocations++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Migration_creates_the_korkut_schema_and_job_tables()
    {
        await using var db = pg.NewContext();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT table_name FROM information_schema.tables WHERE table_schema='korkut' ORDER BY table_name";
        var tables = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Contains("outbox_messages", tables);
        Assert.Contains("inbox_entries", tables);
        Assert.Contains("job_audit", tables);
    }

    [Fact]
    public async Task Inbox_composite_key_prevents_duplicate_effects()
    {
        var office = Guid.CreateVersion7();
        await using (var db = pg.NewContext())
        {
            db.Inbox.Add(new InboxEntry(office, "k", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        await using var db2 = pg.NewContext();
        db2.Inbox.Add(new InboxEntry(office, "k", DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Ef_outbox_round_trips_through_the_dispatcher_on_real_postgres()
    {
        var office = Guid.CreateVersion7();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var handler = new CompletingHandler("email");

        await using var db = pg.NewContext();
        var outbox = new OutboxStore(db);
        var inbox = new InboxStore(db, clock);
        var audit = new JobAudit(db);
        var dispatcher = new OutboxDispatcher(
            outbox, inbox, audit,
            new Dictionary<string, IJobHandler> { ["email"] = handler },
            new AmbientTenantContext(), new ExponentialRetryPolicy(), clock);

        var message = new OutboxMessage(office, "email", "{}", "welcome", clock.UtcNow, clock.UtcNow);
        var id = message.Id;
        await outbox.EnqueueAsync(message, default);

        var handled = await dispatcher.DispatchOnceAsync("test-worker", 10, TimeSpan.FromMinutes(1));

        Assert.Equal(1, handled);
        Assert.Equal(1, handler.Invocations);
        var row = await db.Outbox.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(JobStatus.Completed, row.Status);
        Assert.True(await db.Inbox.AnyAsync(x => x.OfficeId == office && x.IdempotencyKey == "welcome"));
        Assert.True(await db.JobAudit.AnyAsync(x => x.MessageId == id));
    }

    [Fact]
    public async Task Lease_predicate_uses_the_status_available_at_index()
    {
        var office = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        // Enough rows that the planner prefers the index over a seq scan.
        await using (var db = pg.NewContext())
        {
            for (var i = 0; i < 3000; i++)
            {
                db.Outbox.Add(new OutboxMessage(office, "email", "{}", $"k{i}", now.AddSeconds(i), now));
            }

            await db.SaveChangesAsync();
        }

        await using var ctx = pg.NewContext();
        var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        await using (var analyze = conn.CreateCommand())
        {
            analyze.CommandText = "ANALYZE korkut.outbox_messages";
            await analyze.ExecuteNonQueryAsync();
        }

        await using var explain = conn.CreateCommand();
        explain.CommandText = @"EXPLAIN SELECT id FROM korkut.outbox_messages
            WHERE status IN ('Pending','Failed') AND available_at <= now()
            ORDER BY available_at LIMIT 32";
        var plan = new System.Text.StringBuilder();
        await using (var reader = await explain.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                plan.AppendLine(reader.GetString(0));
            }
        }

        var text = plan.ToString();
        Assert.Contains("ix_outbox_status_available_at", text);
        Assert.DoesNotContain("Seq Scan", text);
    }
}
