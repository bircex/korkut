using Korkut.Application.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

public static class PersistenceSetup
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";
    public const string Schema = "korkut";

    /// <summary>The Agent DB's own schema (own migration chain + RLS), separate from the operational schema.</summary>
    public const string AgentSchema = "korkut_agent";

    /// <summary>The Knowledge (RAG) DB's own schema (own migration chain + RLS + pgvector).</summary>
    public const string KnowledgeSchema = "korkut_knowledge";

    /// <summary>
    /// Register the DbContext + EF-backed job stores. Migrations are never applied
    /// here — the API/Worker run under the least-privileged app role and must not
    /// migrate on startup; only Korkut.Migrator applies schema changes (KDM-011).
    /// </summary>
    public static IServiceCollection AddKorkutPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<KorkutDbContext>(options =>
            options.UseNpgsql(ToNpgsql(connectionString), npgsql =>
                npgsql.MigrationsHistoryTable(MigrationsHistoryTable, Schema)));

        services.AddScoped<IOutboxStore, EfOutboxStore>();
        services.AddScoped<IInboxStore, EfInboxStore>();
        services.AddScoped<IJobAudit, EfJobAudit>();
        return services;
    }

    /// <summary>
    /// Register the Agent DB context (assistant's private store, schema <c>korkut_agent</c>).
    /// Its own migration-history table lives in that schema, so it never collides with the
    /// operational chain even when the two share a database. Migrations are applied only by
    /// Korkut.Migrator, never on startup.
    /// </summary>
    public static IServiceCollection AddKorkutAgentPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<Agent.AgentDbContext>(options =>
            options.UseNpgsql(ToNpgsql(connectionString), npgsql =>
                npgsql.MigrationsHistoryTable(MigrationsHistoryTable, AgentSchema)));
        return services;
    }

    /// <summary>
    /// Register the Knowledge DB context (RAG store, schema <c>korkut_knowledge</c>, pgvector
    /// enabled). Own migration-history table in that schema so it never collides with the other
    /// chains when co-located. Migrations are applied only by Korkut.Migrator, never on startup.
    /// </summary>
    public static IServiceCollection AddKorkutKnowledgePersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<Knowledge.KnowledgeDbContext>(options =>
            options.UseNpgsql(ToNpgsql(connectionString), npgsql =>
            {
                npgsql.MigrationsHistoryTable(MigrationsHistoryTable, KnowledgeSchema);
                npgsql.UseVector();
            }));
        return services;
    }

    /// <summary>
    /// Accept either an Npgsql key/value connection string or a
    /// <c>postgres(ql)://user:pass@host:port/db</c> URL (the shape used by the
    /// existing KORKUT_DB_URL / KORKUT_ADMIN_DB_URL). Full typed-Options binding to
    /// the exact env names is finalized in KDM-033.
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
