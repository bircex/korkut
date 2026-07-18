namespace Korkut.Infrastructure;

/// <summary>
/// Assembly marker for the Infrastructure layer (EF Core/Npgsql persistence,
/// MinIO, external providers). Implements Application/Domain abstractions. The
/// concrete DbContext, RLS wiring and providers are added in KDM-011+.
/// </summary>
public static class InfrastructureAssembly;
