using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Korkut.Agent.Persistence;

/// <summary>
/// Registers the Agent DB context (assistant's private store, schema <c>korkut_agent</c>). Its own
/// migration-history table lives in that schema, so it never collides with the operational chain
/// even when the two share a database. Migrations are applied only by Korkut.Migrator, never on startup.
/// </summary>
public static class AgentPersistenceSetup
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";
    public const string AgentSchema = "korkut_agent";

    public static IServiceCollection AddKorkutAgentPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AgentDbContext>(options =>
            options.UseNpgsql(ToNpgsql(connectionString), npgsql =>
                npgsql.MigrationsHistoryTable(MigrationsHistoryTable, AgentSchema)));
        return services;
    }

    /// <summary>Accept an Npgsql key/value string or a <c>postgres(ql)://…</c> URL.</summary>
    public static string ToNpgsql(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
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
