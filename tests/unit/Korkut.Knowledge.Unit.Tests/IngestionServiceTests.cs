using Korkut.Contract.Pii;
using Korkut.Core;

namespace Korkut.Knowledge.Tests;

/// <summary>
/// Ingested text can be document-extraction output (a misread PDF/OCR page) or arbitrary caller
/// input — neither is trusted not to carry a NUL byte, which would otherwise reach
/// ChunkEmbedding's Postgres "text" column and fail with 22021.
/// </summary>
public class IngestionServiceTests
{
    [Fact]
    public async Task IngestAsync_StripsNulBytesBeforeRedactionAndChunking()
    {
        var chunker = new RecordingChunker();
        var svc = new IngestionService(
            chunker, new FakeEmbedder(), new RecordingVectorStore(), new PassthroughRedactor(),
            new InlineUnitOfWork(), new FakeCurrentUser());

        await svc.IngestAsync(Guid.CreateVersion7(), "before\0after", default);

        Assert.Single(chunker.ReceivedTexts);
        Assert.DoesNotContain('\0', chunker.ReceivedTexts[0]);
        Assert.Equal("beforeafter", chunker.ReceivedTexts[0]);
    }

    private sealed class RecordingChunker : IChunkerService
    {
        public List<string> ReceivedTexts { get; } = [];

        public IReadOnlyList<string> Chunk(string text)
        {
            ReceivedTexts.Add(text);
            return [text];
        }
    }

    private sealed class FakeEmbedder : IEmbedder
    {
        public Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => Task.FromResult(new EmbeddingResult(texts.Select(_ => new float[1]).ToList(), "fake", "v1", 1, 0));
    }

    private sealed class RecordingVectorStore : IVectorStoreService
    {
        public Task ReplaceDocumentAsync(Guid officeId, Guid documentId, IReadOnlyList<ChunkEmbedding> chunks, string embeddingVersion, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<SearchHit>> SearchAsync(Guid officeId, float[] query, int topK, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SearchHit>>([]);

        public Task DeleteDocumentAsync(Guid officeId, Guid documentId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class PassthroughRedactor : IPiiRedactorService
    {
        public RedactionResult Redact(string text) => new(text, []);

        public IPiiRedactionScope NewScope() => throw new NotSupportedException();
    }

    private sealed class InlineUnitOfWork : IUnitOfWork
    {
        public Task<T> InTenantScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default) => work();
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid OfficeId => Guid.CreateVersion7();

        public Guid UserId => Guid.CreateVersion7();

        public IReadOnlySet<string> Permissions => new HashSet<string>();
    }
}
