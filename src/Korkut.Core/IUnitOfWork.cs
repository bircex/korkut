namespace Korkut.Core;

/// <summary>
/// Runs a use-case inside a tenant-pinned transaction: the office is set for the duration
/// so Postgres RLS scopes every statement, and the work commits as one unit. This is the
/// only way application services reach the database, so no use-case can skip the guard.
/// </summary>
public interface IUnitOfWork
{
    Task<T> InTenantScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default);
}
