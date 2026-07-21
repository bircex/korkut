using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Korkut.Infrastructure.Persistence.Agent;

/// <summary>
/// Design-time factory so `dotnet ef migrations` can build the Agent context without a
/// running host. Its migrations history lives in the agent schema, so the two contexts'
/// chains never collide even when they share a database. Placeholder connection (never opened).
/// </summary>
public sealed class AgentDbContextFactory : IDesignTimeDbContextFactory<AgentDbContext>
{
    public AgentDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("KORKUT_AGENT_ADMIN_DB_URL")
            ?? Environment.GetEnvironmentVariable("KORKUT_ADMIN_DB_URL")
            ?? "Host=localhost;Port=5432;Database=korkut;Username=korkut_migrator;Password=placeholder";

        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseNpgsql(PersistenceSetup.ToNpgsql(connection), npgsql =>
                npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, PersistenceSetup.AgentSchema))
            .Options;

        return new AgentDbContext(options);
    }
}
