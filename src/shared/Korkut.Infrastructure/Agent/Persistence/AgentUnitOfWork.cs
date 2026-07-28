using Korkut.Contract.Agent;
using Korkut.Persistence;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Agent;

namespace Korkut.Infrastructure.Agent.Persistence;

/// <summary>
/// Owns the DbContext transaction(s) and pins the tenant (RLS) for the duration of a unit of work.
/// The two contexts are separate connections, so a turn that reads operational data (persona/style)
/// while writing Agent state pins BOTH. This is the persistence boundary; no service holds a context.
/// </summary>
public sealed class AgentUnitOfWork(AgentDbContext agent, KorkutDbContext office) : IAgentUnitOfWork
{
    public async Task<T> InScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default)
    {
        await using var scope = await agent.BeginTenantScopeAsync(officeId, ct);
        var result = await work();
        await scope.CommitAsync(ct);
        return result;
    }

    public async Task<T> InDualScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default)
    {
        await using var agentScope = await agent.BeginTenantScopeAsync(officeId, ct);
        await using var officeScope = await office.BeginTenantScopeAsync(officeId, ct);
        var result = await work();
        await officeScope.CommitAsync(ct);
        await agentScope.CommitAsync(ct);
        return result;
    }
}
