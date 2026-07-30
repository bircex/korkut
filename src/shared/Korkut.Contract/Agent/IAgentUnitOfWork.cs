namespace Korkut.Contract.Agent;

/// <summary>
/// The tenant-scoped unit of work: runs a piece of work with the caller's office pinned (Postgres
/// RLS) on the Agent DB — and, for turns that also read operational data, on BOTH databases. This
/// is the ONLY seam that owns the DbContext transaction; services never touch a DbContext, they run
/// their work inside a scope.
/// </summary>
public interface IAgentUnitOfWork
{
    /// <summary>Run <paramref name="work"/> with the office pinned on the Agent DB, then commit.</summary>
    Task<T> InScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default);

    /// <summary>Run <paramref name="work"/> with the office pinned on the operational DB only, then
    /// commit. Use for work that touches only operational tables (conversations/messages, persona,
    /// style, memory) — it pins exactly one connection instead of two.</summary>
    Task<T> InOfficeScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default);

    /// <summary>Run <paramref name="work"/> with the office pinned on BOTH the Agent + operational DBs.</summary>
    Task<T> InDualScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default);
}
