namespace Korkut.Contract.Documents;

/// <summary>
/// The search-index outbox contract (KDM-028). Producers enqueue these; a future
/// indexer consumes them. Deliberately transport-agnostic — no Elasticsearch/vector
/// adapter lives in the codebase. The payload carries full provenance (office +
/// document + version) so the projection never loses its tenant/source origin.
/// </summary>
public static class DocumentIndexEventTypes
{
    public const string Index = "document.index";
    public const string Deindex = "document.deindex";
}

/// <summary>Payload of a <see cref="DocumentIndexEventTypes"/> outbox message.</summary>
public sealed record DocumentIndexEvent(Guid OfficeId, Guid DocumentId, int DocumentVersion, string Op);
