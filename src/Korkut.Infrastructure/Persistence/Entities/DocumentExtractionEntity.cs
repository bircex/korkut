namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// One immutable, provenance-tracked text span extracted from a clean document
/// (KDM-027). Uniqueness over (document, document_version, parser_version, page,
/// span_index) makes re-runs idempotent, and keying on parser_version means a
/// parser change produces a NEW artifact set rather than overwriting the old one.
/// These are search-ready projections only — no external search engine.
/// </summary>
public sealed class DocumentExtractionEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid DocumentId { get; set; }

    /// <summary>The source document's version at extraction time (provenance).</summary>
    public int DocumentVersion { get; set; }

    /// <summary>Stable id of the parser that produced this artifact (immutable version key).</summary>
    public string ParserVersion { get; set; } = string.Empty;

    public int Page { get; set; }

    public int SpanIndex { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Checksum of the source bytes this span was derived from (tamper evidence).</summary>
    public string SourceChecksumSha256 { get; set; } = string.Empty;

    /// <summary>Opaque source object key at extraction time (provenance, no PII).</summary>
    public string SourceObjectKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
