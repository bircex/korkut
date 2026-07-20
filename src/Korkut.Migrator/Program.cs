// Korkut.Migrator — the ONLY process that applies database schema changes, run
// under a separate least-privileged migrator identity (KORKUT_ADMIN_DB_URL), never
// the runtime app role. The API and Worker never migrate on startup (KDM-011).

using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

switch (command)
{
    case "migrate":
    {
        await using var db = BuildContext();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            Console.WriteLine("Korkut.Migrator: no pending migrations.");
            return 0;
        }

        Console.WriteLine($"Korkut.Migrator: applying {pending.Count} migration(s): {string.Join(", ", pending)}");
        await db.Database.MigrateAsync();
        Console.WriteLine("Korkut.Migrator: done.");
        return 0;
    }

    case "info":
    {
        await using var db = BuildContext();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        Console.WriteLine($"Korkut.Migrator: applied={applied.Count} pending={pending.Count}");
        foreach (var m in pending)
        {
            Console.WriteLine($"  pending: {m}");
        }

        return 0;
    }

    default:
        Console.WriteLine("Usage: Korkut.Migrator <migrate|info>");
        Console.WriteLine("  migrate  Apply pending EF Core migrations (migrator role).");
        Console.WriteLine("  info     Print applied/pending migration status.");
        return command == "help" ? 0 : 1;
}
