namespace Korkut.Core;

/// <summary>
/// Resolves operational user identity (display name / email) by id. The Agent host stores
/// only the raw author id in its own DB (the Agent DB never joins operational data), so it
/// resolves the id to a human-facing name/email through this port when shaping a response.
/// The adapter reads the operational store under the caller's tenant scope (RLS). Distinct
/// from <see cref="Korkut.Contract.Security.IUserDirectory"/>, the auth-flow persistence
/// boundary.
/// </summary>
public interface IUserLookupService
{
    /// <summary>
    /// Map each requested user id (in the given office) to a display name. Ids with no match
    /// are simply absent from the result; an empty input yields an empty map.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(
        Guid officeId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    /// <summary>The user's e-mail (used to attribute a saved/accepted document version); null if unknown.</summary>
    Task<string?> EmailAsync(Guid officeId, Guid userId, CancellationToken ct = default);
}
