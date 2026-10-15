using Korkut.Core;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Persistence;

/// <summary>
/// EF/Postgres <see cref="IUnitOfWork"/>: opens a transaction, pins
/// <c>app.current_office</c> for its duration (RLS), runs the work and commits. When a member is
/// behind the scope, the matter-bound tables are narrowed to their matters too.
/// </summary>
public sealed class UnitOfWork(DbContext db, ICurrentUser? currentUser = null) : IUnitOfWork
{
    public async Task<T> InTenantScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default)
    {
        // Reentrancy: when a tenant transaction is already open on this connection, reuse it instead
        // of opening a nested one (Npgsql forbids nesting on a single connection). This is the agent
        // host case — a whole chat turn runs inside AgentUnitOfWork's dual scope (which already pins
        // app.current_office to this same office), then a practice-management tool re-enters here.
        // The outer owner commits; the inner work simply joins the pinned transaction. The API path
        // (no ambient transaction) still opens and commits its own scope as before.
        if (db.Database.CurrentTransaction is not null)
        {
            return await work();
        }

        await using var scope = await db.BeginTenantScopeAsync(officeId, Tenancy.MemberOf(currentUser, officeId), ct);
        var result = await work();
        await scope.CommitAsync(ct);
        return result;
    }
}
