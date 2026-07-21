using System.Security.Claims;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Web.Tenancy;

namespace Korkut.Agent.Tenancy;

/// <summary>
/// Tenant-scoping helpers for the Agent host, which spans TWO databases: the Agent DB
/// (conversations + drafted documents) and the operational DB (office persona / style /
/// memory that a chat or drafting turn composes into its system prompt). A turn that touches
/// both must pin the tenant on BOTH contexts so Postgres RLS scopes each — the two are
/// separate connections, so one <c>set_config</c> does not carry across.
/// </summary>
public static class AgentScope
{
    /// <summary>
    /// Run <paramref name="work"/> with the caller's office pinned on BOTH the Agent DB and
    /// the operational DB. Use for chat / streaming / upload / drafting, which read office
    /// settings (operational) while reading/writing conversation + document state (Agent DB).
    /// </summary>
    public static Task<T> InAgentAndOfficeScopeAsync<T>(
        this ClaimsPrincipal user, AgentDbContext agent, KorkutDbContext office,
        Func<Guid, Task<T>> work, CancellationToken ct = default)
        => user.InTenantScopeAsync(agent, o => user.InTenantScopeAsync(office, _ => work(o), ct), ct);
}
