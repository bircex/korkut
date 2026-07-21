namespace Korkut.Agent.Tools;

/// <summary>
/// The office-scoped context every tool call runs in. Tenant (<see cref="OfficeId"/>) is
/// MANDATORY — a tool always acts within one office (RLS); it is never inferred from arguments.
/// <see cref="Permissions"/> are the caller's granted permissions (the token's <c>perm</c> claims),
/// so a tool the user lacks the right for is neither offered nor executed.
/// </summary>
public sealed record ToolContext(Guid OfficeId, Guid UserId, IReadOnlySet<string> Permissions)
{
    /// <summary>True when the tool needs no permission, or the caller holds the required one.</summary>
    public bool Can(string? permission) => permission is null || Permissions.Contains(permission);
}
