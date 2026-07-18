using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Korkut.Infrastructure.Tests;

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

    public KorkutDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<KorkutDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, PersistenceSetup.Schema))
            .Options;
        return new KorkutDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>Truncate all job tables so each test starts from a clean, isolated state.</summary>
    public async Task ResetAsync()
    {
        await using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE korkut.outbox_messages, korkut.inbox_entries, korkut.job_audit RESTART IDENTITY");
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
