namespace Korkut.Contract.Organization;

/// <summary>
/// Cross-module batch read resolving user ids to a display name (full name, e-mail fallback), so
/// another module can attribute a row (an assignee, an author) without touching the User entity
/// type. Reads in the caller's already-open tenant scope (RLS keeps it to the office).
/// </summary>
public interface IUserLookup
{
    /// <summary>User id → display name. Ids with no user are simply absent; an empty input yields an empty map.</summary>
    Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(
        Guid officeId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    /// <summary>The member's role ("owner", "lawyer", ...), or null when they are not in the office.</summary>
    Task<string?> RoleAsync(Guid officeId, Guid userId, CancellationToken ct = default);

    /// <summary>The office owner's e-mail (the earliest owner when there are several).</summary>
    Task<string?> OwnerEmailAsync(Guid officeId, CancellationToken ct = default);
}
