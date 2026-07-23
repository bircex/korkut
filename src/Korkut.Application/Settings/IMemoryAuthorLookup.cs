namespace Korkut.Application.Settings;

/// <summary>
/// Resolves memory-item authors to the e-mail shown next to their note. Reads run in the
/// caller's already-open tenant scope, which is why this exists alongside
/// <see cref="Korkut.Application.Directory.IUserLookupService"/> — that one opens its own
/// scope, for the Agent host reading across databases.
/// </summary>
public interface IMemoryAuthorLookup
{
    /// <summary>Author id → e-mail. Ids with no user are simply absent; an empty input yields an empty map.</summary>
    Task<IReadOnlyDictionary<Guid, string>> EmailsAsync(
        Guid officeId, IReadOnlyCollection<Guid> authorIds, CancellationToken ct = default);
}
