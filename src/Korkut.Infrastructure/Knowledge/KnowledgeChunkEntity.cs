using Pgvector;
using Korkut.Core;

namespace Korkut.Infrastructure.Knowledge;

/// <summary>
/// The stored form of a <see cref="Korkut.Domain.Knowledge.KnowledgeChunk"/> in the Knowledge DB
/// (schema <c>korkut_knowledge</c>). This row stays in Infrastructure, not Domain, precisely
/// because of <see cref="Embedding"/>: it is a pgvector value, and the Domain must not depend on a
/// persistence-technology type. The row is tenant-scoped by <see cref="OfficeId"/> (RLS) and
/// stamped with the <see cref="EmbeddingVersion"/> so a re-index under a new embedder never mixes
/// vector spaces.
/// </summary>
public sealed class KnowledgeChunkEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid DocumentId { get; set; }

    /// <summary>0-based position of this chunk within its document.</summary>
    public int Ordinal { get; set; }

    public string Text { get; set; } = string.Empty;

    public string EmbeddingVersion { get; set; } = string.Empty;

    /// <summary>The embedding (pgvector). Dimension is fixed by the configured embedder, not the schema.</summary>
    public Vector Embedding { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
