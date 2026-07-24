using Korkut.Core;
using Korkut.Infrastructure.Persistence;

namespace Korkut.Infrastructure.Common;

/// <summary>
/// EF/Postgres <see cref="IUnitOfWork"/>: opens a transaction, pins
/// <c>app.current_office</c> for its duration (RLS), runs the work and commits.
/// </summary>
public sealed class UnitOfWork(KorkutDbContext db) : IUnitOfWork
{
    public async Task<T> InTenantScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default)
    {
        await using var scope = await db.BeginTenantScopeAsync(officeId, ct);
        var result = await work();
        await scope.CommitAsync(ct);
        return result;
    }
}
