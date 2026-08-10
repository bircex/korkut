namespace Korkut.Contract.Clients;

/// <summary>
/// Cross-module batch read resolving client ids to their display name, so another module can
/// enrich a row without ever touching the Client entity type. Reads in the caller's already-open
/// tenant scope (RLS keeps it to the office).
/// </summary>
public interface IClientLookup
{
    /// <summary>Client id → name. Ids with no client are simply absent; an empty input yields an empty map.</summary>
    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(
        Guid officeId, IReadOnlyCollection<Guid> clientIds, CancellationToken ct = default);

    /// <summary>The office's active (non-deleted) client count, for the dashboard.</summary>
    Task<int> CountAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Whether an active client with this id exists in the office.</summary>
    Task<bool> ExistsAsync(Guid officeId, Guid clientId, CancellationToken ct = default);

    /// <summary>The client's current display name, or null if no such client in the office. Used
    /// to snapshot the name onto a linking record (e.g. a matter) at bind time.</summary>
    Task<string?> GetNameAsync(Guid officeId, Guid clientId, CancellationToken ct = default);

    /// <summary>The client's full brief, or null if no such client in the office.</summary>
    Task<ClientResponse?> BriefAsync(Guid officeId, Guid clientId, CancellationToken ct = default);
}
