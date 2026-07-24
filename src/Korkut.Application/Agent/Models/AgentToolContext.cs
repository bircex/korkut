using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Models;

/// <summary>
/// The trusted identity a tool call runs under: the office (tenant) it is pinned to, the acting
/// user, and that user's effective permission set (for tool RBAC). Built from the access token at
/// the edge — never from a request body.
/// </summary>
public sealed record AgentToolContext(Guid OfficeId, Guid UserId, IReadOnlySet<string> Permissions)
{
    /// <summary>True when the tool needs no permission, or the caller holds the required one.</summary>
    public bool Can(string? permission) => permission is null || Permissions.Contains(permission);
}
