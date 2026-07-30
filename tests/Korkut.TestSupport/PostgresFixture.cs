using Korkut.Infrastructure.Persistence;
using Korkut.Agent.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Korkut.TestSupport;

/// <summary>
/// Spins a real PostgreSQL 16 container once per test class and applies the EF
/// migrations, so schema/constraint/query-plan behavior is verified against the
/// actual database engine — never an in-memory substitute (program rule).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Connection string for the least-privileged app role (subject to RLS).</summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    /// <summary>App-role connection with pooling disabled — always a fresh physical connection.</summary>
    public string AppConnectionStringFresh { get; private set; } = string.Empty;

    public KorkutDbContext NewContext() => NewContext(ConnectionString);

    /// <summary>A DbContext connected as the least-privileged app role (subject to RLS).</summary>
    public KorkutDbContext NewAppContext() => NewContext(AppConnectionString);

    private static KorkutDbContext NewContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<KorkutDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, PersistenceSetup.Schema))
            .Options;
        return new KorkutDbContext(options);
    }

    /// <summary>An Agent-DB context (schema korkut_agent) as the superuser.</summary>
    public AgentDbContext NewAgentContext() => NewAgentContext(ConnectionString);

    /// <summary>An Agent-DB context connected as the least-privileged app role (subject to RLS).</summary>
    public AgentDbContext NewAgentAppContext() => NewAgentContext(AppConnectionString);

    private static AgentDbContext NewAgentContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, AgentPersistenceSetup.AgentSchema))
            .Options;
        return new AgentDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        // The Agent DB shares this container (schema korkut_agent, own migration chain);
        // apply it AFTER the operational schema so korkut_app already exists to grant to.
        await using (var agent = NewAgentContext())
        {
            await agent.Database.MigrateAsync();
        }

        // The migration creates korkut_app NOLOGIN; provision a test-only login so we
        // can connect AS the non-superuser app role (RLS is bypassed by superusers).
        await db.Database.ExecuteSqlRawAsync("ALTER ROLE korkut_app WITH LOGIN PASSWORD 'approle_test'");
        AppConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "korkut_app",
            Password = "approle_test",
            Pooling = true,
            MaxPoolSize = 1, // force pooled-connection reuse for the leakage test
        }.ConnectionString;
        AppConnectionStringFresh = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "korkut_app",
            Password = "approle_test",
            Pooling = false, // a genuinely fresh physical connection every time
        }.ConnectionString;
    }

    /// <summary>Truncate all job tables so each test starts from a clean, isolated state.</summary>
    public async Task ResetAsync()
    {
        await using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE korkut.outbox_messages, korkut.inbox_entries, korkut.job_audit, " +
            "korkut.matter_hearings, korkut.matters, korkut.offices, korkut.audit_events, korkut.users, korkut.password_reset_tokens, korkut.user_totp, korkut.recovery_codes, korkut.refresh_tokens, korkut.office_custom_roles, korkut.clients, korkut.courts, korkut.parties, korkut.tasks, korkut.deadlines, korkut.communication_notes, korkut.document_extractions, korkut.document_relations, korkut.invites, korkut.documents, korkut.conversations, korkut.chat_messages " +
            "RESTART IDENTITY CASCADE");
    }

    /// <summary>Truncate the Agent-DB tables so each agent test starts clean.</summary>
    public async Task ResetAgentAsync()
    {
        await using var db = NewAgentContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE korkut_agent.document_versions, korkut_agent.generated_documents RESTART IDENTITY CASCADE");
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
