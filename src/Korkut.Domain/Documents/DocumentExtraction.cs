using Korkut.Domain.Common;

namespace Korkut.Domain.Documents;

/// <summary>
/// One immutable, provenance-tracked text span extracted from a clean document. Uniqueness over
/// (document, document version, parser version, page, span index) makes re-runs idempotent, and
/// keying on the parser version means a parser change produces a NEW artifact set instead of
/// overwriting the old one. These are search-ready projections; no external search engine is
/// involved. An extraction is never mutated after it is written, which is why the type carries no
/// setters beyond construction.
/// </summary>
public sealed class DocumentExtraction : Entity, ITenantOwned, IAggregateRoot
{
    public DocumentExtraction(
        Guid officeId, Guid documentId, int documentVersion, string parserVersion,
        int page, int spanIndex, string text, string sourceChecksumSha256, string sourceObjectKey,
        DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        DocumentId = documentId;
        DocumentVersion = documentVersion;
        ParserVersion = parserVersion;
        Page = page;
        SpanIndex = spanIndex;
        Text = text;
        SourceChecksumSha256 = sourceChecksumSha256;
        SourceObjectKey = sourceObjectKey;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private DocumentExtraction()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid DocumentId { get; private set; }

    /// <summary>The source document's version at extraction time (provenance).</summary>
    public int DocumentVersion { get; private set; }

    /// <summary>Stable id of the parser that produced this artifact (immutable version key).</summary>
    public string ParserVersion { get; private set; } = string.Empty;

    public int Page { get; private set; }

    public int SpanIndex { get; private set; }

    public string Text { get; private set; } = string.Empty;

    /// <summary>Checksum of the source bytes this span was derived from (tamper evidence).</summary>
    public string SourceChecksumSha256 { get; private set; } = string.Empty;

    /// <summary>Opaque source object key at extraction time (provenance, no PII).</summary>
    public string SourceObjectKey { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
