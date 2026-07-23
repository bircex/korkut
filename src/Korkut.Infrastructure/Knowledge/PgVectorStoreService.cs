using Korkut.Application.Knowledge;
using Korkut.Domain.Common;
using Korkut.Domain.Knowledge;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Infrastructure.Persistence.Knowledge;
using Korkut.Domain.Time;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Korkut.Infrastructure.Knowledge;

/// <summary>
/// pgvector-backed <see cref="IVectorStoreService"/> over the Knowledge DB. Tenant context is set by the
/// caller (RLS on <c>app.current_office</c>); the office is still passed to stamp rows and scope
/// queries explicitly. Similarity is cosine via pgvector's <c>&lt;=&gt;</c> operator (exposed as
/// <see cref="VectorExtensions.CosineDistance"/> in LINQ); the returned score is
/// <c>1 - cosine_distance</c> so higher is closer. Re-indexing a document deletes its prior chunks
/// first, so stale vectors never survive an embedder/chunker change.
/// </summary>
public sealed class PgVectorStoreService(KnowledgeDbContext db, IClock clock) : IVectorStoreService
{
    public async Task ReplaceDocumentAsync(
        Guid officeId, Guid documentId, IReadOnlyList<ChunkEmbedding> chunks, string embeddingVersion, CancellationToken ct = default)
    {
        await db.Chunks.Where(c => c.OfficeId == officeId && c.DocumentId == documentId).ExecuteDeleteAsync(ct);

        var now = clock.UtcNow;
        foreach (var chunk in chunks)
        {
            db.Chunks.Add(new KnowledgeChunkEntity
            {
                Id = UuidV7.NewId(),
                OfficeId = officeId,
                DocumentId = documentId,
                Ordinal = chunk.Ordinal,
                Text = chunk.Text,
                EmbeddingVersion = embeddingVersion,
                Embedding = new Vector(chunk.Vector),
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(Guid officeId, float[] query, int topK, CancellationToken ct = default)
    {
        var q = new Vector(query);
        var rows = await db.Chunks.AsNoTracking()
            .Where(c => c.OfficeId == officeId)
            .OrderBy(c => c.Embedding.CosineDistance(q))
            .Take(topK)
            .Select(c => new { c.Id, c.DocumentId, c.Ordinal, c.Text, Distance = c.Embedding.CosineDistance(q) })
            .ToListAsync(ct);

        return rows.Select(r => new SearchHit(r.Id, r.DocumentId, r.Ordinal, r.Text, 1.0 - r.Distance)).ToList();
    }

    public Task DeleteDocumentAsync(Guid officeId, Guid documentId, CancellationToken ct = default)
        => db.Chunks.Where(c => c.OfficeId == officeId && c.DocumentId == documentId).ExecuteDeleteAsync(ct);
}
