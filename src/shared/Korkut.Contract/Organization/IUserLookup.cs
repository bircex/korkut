namespace Korkut.Contract.Organization;

/// <summary>
/// Cross-module batch read resolving user ids to their e-mail, so another module can attribute a
/// row (an assignee, an author) without touching the User entity type. Reads in the caller's
/// already-open tenant scope (RLS keeps it to the office).
/// </summary>
public interface IUserLookup
{
    /// <summary>User id → e-mail. Ids with no user are simply absent; an empty input yields an empty map.</summary>
    Task<IReadOnlyDictionary<Guid, string>> EmailsAsync(
        Guid officeId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}
