// Korkut.Migrator — the ONLY process permitted to apply database schema changes,
// run under a separate least-privileged migrator identity (never the app role).
// Migrations are applied explicitly by invoking this tool during deploy — the API
// and Worker never auto-migrate on startup (per the architecture rules).
//
// The concrete expand/contract EF Core migration runner is implemented in KDM-011;
// this scaffold establishes the separate host and its explicit, opt-in command
// surface without performing any schema change yet.

var command = args.Length > 0 ? args[0] : "help";

switch (command)
{
    case "migrate":
        Console.Error.WriteLine(
            "Korkut.Migrator: no migrations defined yet (added in KDM-011). Nothing applied.");
        return 0;

    case "info":
        Console.WriteLine("Korkut.Migrator: separate least-privileged schema-migration host.");
        Console.WriteLine("No migrations are defined in the scaffold (KDM-005).");
        return 0;

    default:
        Console.WriteLine("Usage: Korkut.Migrator <migrate|info>");
        Console.WriteLine("  migrate  Apply pending migrations (none until KDM-011).");
        Console.WriteLine("  info     Print migrator status.");
        return command == "help" ? 0 : 1;
}
