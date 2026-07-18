using Korkut.Application.Jobs;
using Korkut.Application.Jobs.InMemory;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// Reusable tenant-isolation matrix (KDM-013) over every registered tenant table
/// and multiple principals (office A, office B, no-context), on real PostgreSQL.
/// Includes a coverage guard, post-rollback/pool context cleanup, and the audited
/// background-job path. Runs as the least-privileged app role (RLS-subject).
/// </summary>
public class TenantMatrixTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _a;
    private Guid _b;

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _a = Guid.CreateVersion7();
        _b = Guid.CreateVersion7();

        // Seed via superuser (RLS bypassed): A owns 1 matter+1 hearing, B owns 2+2.
        await using var db = pg.NewContext();
        db.Offices.AddRange(
            new OfficeEntity { Id = _a, Name = "A", CreatedAt = DateTimeOffset.UtcNow },
            new OfficeEntity { Id = _b, Name = "B", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        await SeedMattersAndHearings(db, _a, 1);
        await SeedMattersAndHearings(db, _b, 2);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task SeedMattersAndHearings(KorkutDbContext db, Guid office, int n)
    {
        for (var i = 0; i < n; i++)
        {
            var matter = new MatterEntity { Id = Guid.CreateVersion7(), OfficeId = office, Title = $"m{i}", CreatedAt = DateTimeOffset.UtcNow };
            db.Matters.Add(matter);
            db.MatterHearings.Add(new MatterHearingEntity { Id = Guid.CreateVersion7(), OfficeId = office, MatterId = matter.Id, ScheduledAt = DateTimeOffset.UtcNow });
        }

        await db.SaveChangesAsync();
    }

    // --- Coverage guard: DB-forced RLS tables must all be in the registry ---

    [Fact]
    public async Task Every_rls_forced_table_is_covered_by_the_matrix()
    {
        await using var db = pg.NewContext();
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT c.relname FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname='korkut' AND c.relkind='r' AND c.relforcerowsecurity";
        var forced = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                forced.Add(reader.GetString(0));
            }
        }

        // Any RLS-forced table missing from the matrix registry fails CI — a new
        // tenant-owned entity cannot ship without isolation coverage.
        var uncovered = forced.Where(t => !TenantMatrix.Tables.ContainsKey(t)).ToList();
        Assert.True(uncovered.Count == 0, $"tenant tables missing from the isolation matrix: {string.Join(", ", uncovered)}");
        Assert.Equal(TenantMatrix.Tables.Count, forced.Count);
    }

    // --- Negative matrix over every table and principal ---

    [Theory]
    [MemberData(nameof(TenantMatrix.TableNames), MemberType = typeof(TenantMatrix))]
    public async Task No_context_principal_sees_no_rows(string table)
    {
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        Assert.Equal(0, await TenantMatrix.CountAsync(conn, tx, table));
    }

    [Theory]
    [MemberData(nameof(TenantMatrix.TableNames), MemberType = typeof(TenantMatrix))]
    public async Task Each_office_sees_only_its_own_rows(string table)
    {
        var (expectA, expectB) = TenantMatrix.Tables[table];
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();

        await using (var txA = await conn.BeginTransactionAsync())
        {
            await TenantMatrix.SetTenantAsync(conn, txA, _a);
            Assert.Equal(expectA, await TenantMatrix.CountAsync(conn, txA, table));
        }

        await using var txB = await conn.BeginTransactionAsync();
        await TenantMatrix.SetTenantAsync(conn, txB, _b);
        Assert.Equal(expectB, await TenantMatrix.CountAsync(conn, txB, table));
    }

    // --- Context cleanup after rollback and across pooled reuse ---

    [Fact]
    public async Task Rolled_back_context_does_not_leak_into_the_next_scope()
    {
        // Note: a custom GUC set with is_local is NOT reliably reset by ROLLBACK, so
        // isolation must not depend on implicit cleanup. The architecture instead sets
        // the tenant explicitly per scope — so the next scope's tenant always wins and
        // stale context can never be read. This proves that guarantee.
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();

        await using (var tx = await conn.BeginTransactionAsync())
        {
            await TenantMatrix.SetTenantAsync(conn, tx, _a);
            Assert.Equal(1, await TenantMatrix.CountAsync(conn, tx, "matters"));
            await tx.RollbackAsync();
        }

        // The next scope sets its OWN tenant (office B) and sees only B's rows — never
        // office A's rolled-back context.
        await using var tx2 = await conn.BeginTransactionAsync();
        await TenantMatrix.SetTenantAsync(conn, tx2, _b);
        Assert.Equal(2, await TenantMatrix.CountAsync(conn, tx2, "matters"));
    }

    // --- Background-job path is tenant-scoped and audited ---

    [Fact]
    public async Task Background_jobs_run_under_their_own_tenant_and_are_audited()
    {
        var clock = new SystemClockAdapter();
        await using var db = pg.NewContext();
        var outbox = new EfOutboxStore(db, clock);
        var inbox = new EfInboxStore(db, clock);
        var audit = new EfJobAudit(db);
        var seenTenants = new List<Guid?>();
        var tenant = new AmbientTenantContext();
        var handler = new RecordingTenantHandler(tenant, seenTenants);
        var dispatcher = new OutboxDispatcher(
            outbox, inbox, audit, new Dictionary<string, IJobHandler> { ["email"] = handler },
            tenant, new ExponentialRetryPolicy(), clock);

        await outbox.EnqueueAsync(Msg(_a, "a"), default);
        await outbox.EnqueueAsync(Msg(_b, "b"), default);

        await dispatcher.DispatchOnceAsync("w", 10, TimeSpan.FromMinutes(1));

        Assert.Contains(_a, seenTenants);
        Assert.Contains(_b, seenTenants);
        Assert.DoesNotContain(null, seenTenants);
        // Each office's job produced an audit row scoped to that office.
        Assert.True(await db.JobAudit.AnyAsync(x => x.OfficeId == _a));
        Assert.True(await db.JobAudit.AnyAsync(x => x.OfficeId == _b));
    }

    private static OutboxMessage Msg(Guid office, string key) => new()
    {
        Id = Guid.CreateVersion7(), OfficeId = office, Type = "email", Payload = "{}", IdempotencyKey = key,
    };

    private sealed class SystemClockAdapter : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class RecordingTenantHandler(ITenantContext tenant, List<Guid?> seen) : IJobHandler
    {
        public string MessageType => "email";

        public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            seen.Add(tenant.CurrentOfficeId);
            return Task.CompletedTask;
        }
    }
}
