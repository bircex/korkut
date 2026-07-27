namespace Korkut.Contract.Settings;

/// <summary>
/// Cross-module read resolving memory-item authors to the e-mail shown next to their note. Reads
/// in the caller's already-open tenant scope (RLS keeps it to the office). Impl lives in
/// Infrastructure (it reads the users table, owned by the identity domain).
/// </summary>
public interface IMemoryAuthorLookup
{
    /// <summary>Author id → e-mail. Ids with no user are simply absent; an empty input yields an empty map.</summary>
    Task<IReadOnlyDictionary<Guid, string>> EmailsAsync(
        Guid officeId, IReadOnlyCollection<Guid> authorIds, CancellationToken ct = default);
}
