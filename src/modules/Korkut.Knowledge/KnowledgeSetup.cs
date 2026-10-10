using Korkut.Core;
using Korkut.Knowledge.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace Korkut.Knowledge;

/// <summary>
/// Self-registration for the Knowledge (RAG) module. Owns its schema + migration-history table
/// (so its chain never collides when co-located with the operational/agent schemas) and wires
/// the pgvector store plus the ingestion/retrieval use-case services. The context is registered
/// but NEVER migrated on startup — only Korkut.Migrator applies schema. The embedder
/// (<c>AddKorkutAi</c>), the caller identity (<c>ICurrentUser</c>) and the HTTP plumbing come
/// from the host, so the module stays free of any web/host dependency.
/// </summary>
public static class KnowledgeSetup
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>The Knowledge (RAG) DB's own schema (own migration chain + RLS + pgvector).</summary>
    public const string KnowledgeSchema = "korkut_knowledge";

    public static IServiceCollection AddKorkutKnowledge(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<KnowledgeDbContext>(options =>
            options.UseNpgsql(ToNpgsql(connectionString), npgsql =>
            {
                npgsql.MigrationsHistoryTable(MigrationsHistoryTable, KnowledgeSchema);
                npgsql.UseVector();
            }));

        services.AddSingleton<IChunkerService, ChunkerService>();
        services.AddScoped<IVectorStoreService, PgVectorStoreService>();
        services.AddScoped<IIngestionService, IngestionService>();
        services.AddScoped<IRetrievalService, RetrievalService>();
        // The Knowledge DB has its own unit of work (its own context + tenant scope), under its own
        // interface so it never displaces the operational IUnitOfWork in the same container.
        services.AddScoped<IKnowledgeUnitOfWork, KnowledgeUnitOfWork>();
        // What chat grounding and the search tool consume: retrieval that never fails the turn.
        services.AddScoped<IKnowledgeSearchService, GroundingSearchService>();
        return services;
    }

    /// <summary>
    /// Accept either an Npgsql key/value connection string or a
    /// <c>postgres(ql)://user:pass@host:port/db</c> URL (the KORKUT_*_DB_URL shape).
    /// </summary>
    public static string ToNpgsql(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value; // already a key/value connection string
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };
        return builder.ConnectionString;
    }
}
