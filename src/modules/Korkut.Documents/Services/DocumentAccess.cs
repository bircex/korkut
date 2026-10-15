using Korkut.Contract.Documents;
using Korkut.Contract.Matters;
using Korkut.Documents.Entities;
using Korkut.Documents.Repositories;

namespace Korkut.Documents.Services;

/// <summary>
/// The one rule for who sees a document: whoever uploaded it, the team of any matter it is linked
/// to (directly or through one of the matter's parties), and whoever sees every matter. Used by the
/// document library and, through <see cref="IDocumentAccess"/>, by the search over documents.
/// </summary>
public sealed class DocumentAccess(IDocumentRepository repository, IMatterAccess access, IMatterLookup matters) : IDocumentAccess
{
    public async Task<IReadOnlySet<Guid>?> VisibleDocumentIdsAsync(Guid officeId, Guid? matterId = null, CancellationToken ct = default)
    {
        var scope = await access.ScopeAsync(officeId, ct);
        if (scope.AllMatters && matterId is null)
        {
            return null;
        }

        var (visible, mattersOf) = await VisibilityAsync(officeId, ct);
        return (await repository.ListAsync(officeId, null, ct))
            .Where(visible)
            .Where(d => matterId is not { } id || d.MatterId == id || mattersOf(d).Contains(id))
            .Select(d => d.Id)
            .ToHashSet();
    }

    /// <summary>
    /// Who sees which document, and which matters a document is linked to. The uploader always
    /// sees their own; otherwise a link to a matter in the caller's scope (directly, or through one
    /// of its parties) does.
    /// </summary>
    internal async Task<(Func<Document, bool> Visible, Func<Document, IReadOnlySet<Guid>> MattersOf)> VisibilityAsync(
        Guid office, CancellationToken ct)
    {
        var scope = await access.ScopeAsync(office, ct);
        var links = await repository.RelationsAsync(office, ct);
        var partyMatters = links.Any(l => l.TargetType == DocumentRelationTarget.Party)
            ? await matters.PartyMattersAsync(office, ct)
            : new Dictionary<Guid, Guid>();
        var byDocument = links
            .Select(l => (l.DocumentId, Matter: l.TargetType switch
            {
                DocumentRelationTarget.Matter => l.TargetId,
                DocumentRelationTarget.Party => partyMatters.TryGetValue(l.TargetId, out var m) ? m : (Guid?)null,
                _ => null,
            }))
            .Where(x => x.Matter is not null)
            .GroupBy(x => x.DocumentId)
            .ToDictionary(g => g.Key, g => (IReadOnlySet<Guid>)g.Select(x => x.Matter!.Value).ToHashSet());

        IReadOnlySet<Guid> MattersOf(Document d) => byDocument.TryGetValue(d.Id, out var set) ? set : new HashSet<Guid>();
        bool Visible(Document d) => scope.AllMatters || d.OwnerId == scope.UserId
            || (d.MatterId is { } m && scope.CanSee(m)) || MattersOf(d).Any(scope.CanSee);
        return (Visible, MattersOf);
    }
}
