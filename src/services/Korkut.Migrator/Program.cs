// Korkut.Migrator — the ONLY process that applies database schema changes, run
// under a separate least-privileged migrator identity (KORKUT_ADMIN_DB_URL), never
// the runtime app role. The API and Worker never migrate on startup (KDM-011).
// Applies BOTH schemas: the operational context and the Agent context (korkut_agent).

using Korkut.Infrastructure.Persistence;
using Korkut.Agent.Persistence;
using Korkut.Knowledge;
using Korkut.Knowledge.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

var command = args.Length > 0 ? args[0] : "help";

static KorkutDbContext BuildContext()
{
    var connection = Environment.GetEnvironmentVariable("KORKUT_ADMIN_DB_URL")
        ?? throw new InvalidOperationException(
            "KORKUT_ADMIN_DB_URL is required for the migrator (the elevated, migrator-role connection).");

    var options = new DbContextOptionsBuilder<KorkutDbContext>()
        .UseNpgsql(PersistenceSetup.ToNpgsql(connection), npgsql =>
            npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, PersistenceSetup.Schema))
        .Options;
    return new KorkutDbContext(options);
}

static AgentDbContext BuildAgentContext()
{
    // The Agent DB may be a separate database/instance; falls back to the operational
    // migrator connection when co-located.
    var connection = Environment.GetEnvironmentVariable("KORKUT_AGENT_ADMIN_DB_URL")
        ?? Environment.GetEnvironmentVariable("KORKUT_ADMIN_DB_URL")
        ?? throw new InvalidOperationException(
            "KORKUT_AGENT_ADMIN_DB_URL or KORKUT_ADMIN_DB_URL is required for the agent migrator connection.");

    var options = new DbContextOptionsBuilder<AgentDbContext>()
        .UseNpgsql(PersistenceSetup.ToNpgsql(connection), npgsql =>
            npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, AgentPersistenceSetup.AgentSchema))
        .Options;
    return new AgentDbContext(options);
}

static KnowledgeDbContext BuildKnowledgeContext()
{
    // The Knowledge DB may be a separate database/instance; falls back to the operational
    // migrator connection when co-located. pgvector must be enabled for the vector column.
    var connection = Environment.GetEnvironmentVariable("KORKUT_KNOWLEDGE_ADMIN_DB_URL")
        ?? Environment.GetEnvironmentVariable("KORKUT_ADMIN_DB_URL")
        ?? throw new InvalidOperationException(
            "KORKUT_KNOWLEDGE_ADMIN_DB_URL or KORKUT_ADMIN_DB_URL is required for the knowledge migrator connection.");

    var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
        .UseNpgsql(KnowledgeSetup.ToNpgsql(connection), npgsql =>
        {
            npgsql.MigrationsHistoryTable(KnowledgeSetup.MigrationsHistoryTable, KnowledgeSetup.KnowledgeSchema);
            npgsql.UseVector();
        })
        .Options;
    return new KnowledgeDbContext(options);
}

static async Task<int> MigrateAsync(DbContext db, string label)
{
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count == 0)
    {
        Console.WriteLine($"Korkut.Migrator [{label}]: no pending migrations.");
        return 0;
    }

    Console.WriteLine($"Korkut.Migrator [{label}]: applying {pending.Count} migration(s): {string.Join(", ", pending)}");
    await db.Database.MigrateAsync();
    Console.WriteLine($"Korkut.Migrator [{label}]: done.");
    return 0;
}

static async Task InfoAsync(DbContext db, string label)
{
    var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    Console.WriteLine($"Korkut.Migrator [{label}]: applied={applied.Count} pending={pending.Count}");
    foreach (var m in pending)
    {
        Console.WriteLine($"  pending: {m}");
    }
}

switch (command)
{
    case "migrate":
    {
        await using var ops = BuildContext();
        await MigrateAsync(ops, "operational");
        await using var agent = BuildAgentContext();
        await MigrateAsync(agent, "agent");
        await using var knowledge = BuildKnowledgeContext();
        await MigrateAsync(knowledge, "knowledge");
        return 0;
    }

    case "info":
    {
        await using var ops = BuildContext();
        await InfoAsync(ops, "operational");
        await using var agent = BuildAgentContext();
        await InfoAsync(agent, "agent");
        await using var knowledge = BuildKnowledgeContext();
        await InfoAsync(knowledge, "knowledge");
        return 0;
    }

    default:
        Console.WriteLine("Usage: Korkut.Migrator <migrate|info>");
        Console.WriteLine("  migrate  Apply pending EF Core migrations for both schemas (migrator role).");
        Console.WriteLine("  info     Print applied/pending migration status for both schemas.");
        return command == "help" ? 0 : 1;
}
