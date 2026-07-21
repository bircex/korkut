using Korkut.Infrastructure.Persistence;

namespace Korkut.Agent.Tools;

/// <summary>
/// Operational-DB implementation of <see cref="IOfficeScope"/>: opens a tenant-pinned transaction
/// on the operational <see cref="KorkutDbContext"/> for the office, runs the work, and commits.
/// RLS applies to every query/command inside, so an office-domain tool is structurally office-scoped.
/// </summary>
public sealed class OfficeScope(KorkutDbContext db) : IOfficeScope
{
    public async Task<T> RunAsync<T>(Guid officeId, Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        await using var scope = await db.BeginTenantScopeAsync(officeId, ct);
        var result = await work(ct);
        await scope.CommitAsync(ct);
        return result;
    }
}
