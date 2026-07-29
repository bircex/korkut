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
}
