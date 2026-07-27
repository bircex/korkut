using Korkut.Agent.Drafting;
using Korkut.Agent.Ledger;
using Korkut.Agent.Ledger.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// Tenant-isolation coverage for the Agent DB (schema korkut_agent), mirroring the
/// operational matrix: every agent table forces RLS on the shared <c>app.current_office</c>
/// GUC, so office A never sees office B's drafts/versions/llm-calls. Conversations + messages
/// are a general operational feature now (covered by the operational TenantMatrix). Runs as
/// the least-privileged app role against real PostgreSQL.
/// </summary>
public class AgentTenantIsolationTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly (string Table, int A, int B)[] Expected =
    [
        ("generated_documents", 1, 2),
        ("document_versions", 1, 2),
        ("llm_calls", 1, 2),
    ];

    private Guid _a;
    private Guid _b;

    public async Task InitializeAsync()
    {
        await pg.ResetAgentAsync();
        _a = Guid.CreateVersion7();
        _b = Guid.CreateVersion7();

        // Seed via superuser (RLS bypassed): A owns 1 of each, B owns 2 of each.
        await using var db = pg.NewAgentContext();
        await SeedAsync(db, _a, 1);
        await SeedAsync(db, _b, 2);
    }

    private static async Task SeedAsync(Korkut.Infrastructure.Persistence.Agent.AgentDbContext db, Guid office, int n)
    {
        for (var i = 0; i < n; i++)
        {
            // Conversations live in the operational DB now; drafts reference one by loose id.
            var convId = Guid.CreateVersion7();
            var generated = new GeneratedDocument(office, convId, $"d{i}", DateTimeOffset.UtcNow);
            var docId = generated.Id;
            db.GeneratedDocuments.Add(generated);
            db.DocumentVersions.Add(new DocumentVersion(office, docId, "x", null, null, DateTimeOffset.UtcNow));
            db.LlmCalls.Add(new LlmCall(office, "chat", "fake", "fake-model", "", 1, 1, 0m, 1, DateTimeOffset.UtcNow));
        }

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Every_agent_rls_forced_table_is_covered()
    {
        await using var db = pg.NewAgentContext();
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT c.relname FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname='korkut_agent' AND c.relkind='r' AND c.relforcerowsecurity";
        var forced = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                forced.Add(reader.GetString(0));
            }
        }

        var covered = Expected.Select(e => e.Table).ToHashSet();
        var uncovered = forced.Where(t => !covered.Contains(t)).ToList();
        Assert.True(uncovered.Count == 0, $"agent tables missing isolation coverage: {string.Join(", ", uncovered)}");
        Assert.Equal(Expected.Length, forced.Count);
    }

    [Fact]
    public async Task No_context_principal_sees_no_agent_rows()
    {
        await using var conn = new NpgsqlConnection(pg.AppConnectionStringFresh);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        foreach (var (table, _, _) in Expected)
        {
            Assert.Equal(0, await CountAsync(conn, tx, table));
        }
    }

    [Fact]
    public async Task Each_office_sees_only_its_own_agent_rows()
    {
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();

        foreach (var (table, expectA, expectB) in Expected)
        {
            await using (var txA = await conn.BeginTransactionAsync())
            {
                await SetTenantAsync(conn, txA, _a);
                Assert.Equal(expectA, await CountAsync(conn, txA, table));
            }

            await using var txB = await conn.BeginTransactionAsync();
            await SetTenantAsync(conn, txB, _b);
            Assert.Equal(expectB, await CountAsync(conn, txB, table));
        }
    }

    private static async Task<long> CountAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT count(*) FROM korkut_agent.{table}";
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task SetTenantAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid office)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT set_config('app.current_office', @o, true)";
        cmd.Parameters.AddWithValue("o", office.ToString());
        await cmd.ExecuteNonQueryAsync();
    }
}
