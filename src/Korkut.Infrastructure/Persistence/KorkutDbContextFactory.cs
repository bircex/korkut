using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef migrations` can build the context without a
/// running host. Uses a placeholder connection (never opened during scaffolding).
/// </summary>
public sealed class KorkutDbContextFactory : IDesignTimeDbContextFactory<KorkutDbContext>
{
    public KorkutDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("KORKUT_ADMIN_DB_URL")
            ?? "Host=localhost;Port=5432;Database=korkut;Username=korkut_migrator;Password=placeholder";

        var options = new DbContextOptionsBuilder<KorkutDbContext>()
            .UseNpgsql(PersistenceSetup.ToNpgsql(connection), npgsql =>
                npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, PersistenceSetup.Schema))
            .Options;

        return new KorkutDbContext(options);
    }
}
