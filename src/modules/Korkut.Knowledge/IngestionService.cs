using Korkut.Contract.Pii;
using Korkut.Core;

namespace Korkut.Knowledge;

/// <summary>
/// Ingestion over the ports: PII-mask the text, chunk it, embed the chunks, and replace the
/// document's vectors — stamping the embedder's version so a later embedder change re-indexes
/// under a new space. Masking happens FIRST, so neither the stored chunk text nor its embedding
/// ever holds raw PII (TCKN, phone, e-mail, IBAN); the vector index cannot leak it. The office
/// comes from the caller's token, and the whole chunk → embed → store unit runs in that tenant
/// scope so the embedder call is inside it.
/// </summary>
public sealed class IngestionService(
    IChunkerService chunkerService,
    IEmbedder embedder,
    IVectorStoreService storeService,
    IPiiRedactorService redactorService,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IIngestionService
{
    public Task<int> IngestAsync(Guid documentId, string text, CancellationToken ct = default)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, async () =>
        {
            // Strip bytes Postgres' "text" columns reject before anything else touches this text —
            // the caller can be document-extraction output (a misread PDF/OCR page) or arbitrary
            // ingest input, neither of which can be trusted not to carry a NUL byte.
            var clean = PostgresTextSanitizer.StripInvalidChars(text);
            // Mask PII before anything is chunked, embedded or stored — the index must never hold it.
            var masked = redactorService.Redact(clean).Text;
            var chunks = chunkerService.Chunk(masked);
            if (chunks.Count == 0)
            {
                await storeService.DeleteDocumentAsync(office, documentId, ct);
                return 0;
            }

            var embedding = await embedder.EmbedAsync(chunks, ct);
            var items = new List<ChunkEmbedding>(chunks.Count);
            
            for (var i = 0; i < chunks.Count; i++)
            {
                items.Add(new ChunkEmbedding(i, chunks[i], embedding.Vectors[i]));
            }

            await storeService.ReplaceDocumentAsync(office, documentId, items, embedding.EmbeddingVersion, ct);
            return items.Count;
        }, ct);
    }
}
