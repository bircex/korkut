using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Korkut.Knowledge.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef migrations` can build the Knowledge context without a
/// running host. Its migrations history lives in the knowledge schema, so the chains never
/// collide even when co-located. Enables pgvector so the <c>vector</c> column maps at design time.
/// Placeholder connection (never opened).
/// </summary>
public sealed class KnowledgeDbContextFactory : IDesignTimeDbContextFactory<KnowledgeDbContext>
{
    public KnowledgeDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("KORKUT_KNOWLEDGE_ADMIN_DB_URL")
            ?? Environment.GetEnvironmentVariable("KORKUT_ADMIN_DB_URL")
            ?? "Host=localhost;Port=5432;Database=korkut;Username=korkut_migrator;Password=placeholder";

        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(KnowledgeSetup.ToNpgsql(connection), npgsql =>
            {
                npgsql.MigrationsHistoryTable(KnowledgeSetup.MigrationsHistoryTable, KnowledgeSetup.KnowledgeSchema);
                npgsql.UseVector();
            })
            .Options;

        return new KnowledgeDbContext(options);
    }
}
