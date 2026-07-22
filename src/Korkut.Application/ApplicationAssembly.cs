namespace Korkut.Application;

/// <summary>
/// Assembly marker for the Application layer (command/query handlers, validation,
/// authorization policies). Depends on Domain only — never on
/// Infrastructure, EF Core, or the web host. Concrete handlers are added per
/// module in later KDM issues.
/// </summary>
public static class ApplicationAssembly;
