using Korkut.Contract.Documents;
using Korkut.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace Korkut.Knowledge.Tests;

/// <summary>
/// Chat is grounded on the index when it can be and answers without it when it cannot: an install
/// with no embedding backend, or a database hiccup, must never turn into a failed chat turn.
/// </summary>
public class GroundingSearchServiceTests
{
    private sealed class Retrieval(Func<IReadOnlyList<SearchHit>> answer) : IRetrievalService
    {
        public int? AskedTopK { get; private set; }

        public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, CancellationToken ct = default)
        {
            AskedTopK = topK;
            return Task.FromResult(answer());
        }
    }

    /// <summary>The documents the caller may see; null means every document.</summary>
    private sealed class Access(IReadOnlySet<Guid>? visible) : IDocumentAccess
    {
        public Guid? AskedMatter { get; private set; }

        public Task<IReadOnlySet<Guid>?> VisibleDocumentIdsAsync(Guid officeId, Guid? matterId = null, CancellationToken ct = default)
        {
            AskedMatter = matterId;
            return Task.FromResult(visible);
        }
    }

    private sealed class DirectUnitOfWork : IUnitOfWork
    {
        public Task<T> InTenantScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default) => work();
    }

    private sealed class Caller : ICurrentUser
    {
        public Guid OfficeId { get; } = Guid.NewGuid();

        public Guid UserId { get; } = Guid.NewGuid();

        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private static GroundingSearchService Service(IRetrievalService retrieval, IDocumentAccess? access = null)
        => new(retrieval, access ?? new Access(null), new DirectUnitOfWork(), new Caller(),
            NullLogger<GroundingSearchService>.Instance);

    [Fact]
    public async Task Only_documents_the_caller_may_see_come_back()
    {
        var mine = new SearchHit(Guid.NewGuid(), Guid.NewGuid(), 0, "kira", 0.9);
        var theirs = new SearchHit(Guid.NewGuid(), Guid.NewGuid(), 0, "kira", 0.95);
        var retrieval = new Retrieval(() => [theirs, mine]);

        var hits = await Service(retrieval, new Access(new HashSet<Guid> { mine.DocumentId })).SearchAsync("kira", 3);

        Assert.Equal(mine, Assert.Single(hits));
        Assert.Equal(15, retrieval.AskedTopK); // asks for more, then keeps the visible ones
    }

    [Fact]
    public async Task Nothing_visible_skips_the_index()
    {
        var retrieval = new Retrieval(() => throw new InvalidOperationException("should not be asked"));

        Assert.Empty(await Service(retrieval, new Access(new HashSet<Guid>())).SearchAsync("kira", 3));
        Assert.Null(retrieval.AskedTopK);
    }

    [Fact]
    public async Task A_matter_conversation_asks_for_that_matters_documents()
    {
        var matter = Guid.NewGuid();
        var access = new Access(new HashSet<Guid>());

        await Service(new Retrieval(() => []), access).SearchAsync("kira", 3, matter);

        Assert.Equal(matter, access.AskedMatter);
    }

    [Fact]
    public async Task Hits_pass_through()
    {
        var hit = new SearchHit(Guid.NewGuid(), Guid.NewGuid(), 0, "kira", 0.9);

        var hits = await Service(new Retrieval(() => [hit])).SearchAsync("kira", 3);

        Assert.Equal(hit, Assert.Single(hits));
    }

    [Fact]
    public async Task No_embedding_backend_means_no_grounding_not_a_failed_turn()
    {
        var retrieval = new Retrieval(() => throw new ModelUnavailableException("no embedder"));

        Assert.Empty(await Service(retrieval).SearchAsync("kira", 3));
    }

    [Fact]
    public async Task Any_other_failure_also_answers_ungrounded()
    {
        var retrieval = new Retrieval(() => throw new InvalidOperationException("db down"));

        Assert.Empty(await Service(retrieval).SearchAsync("kira", 3));
    }

    [Fact]
    public async Task A_cancelled_turn_stays_cancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var retrieval = new Retrieval(() => throw new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(retrieval).SearchAsync("kira", 3, null, cts.Token));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(500, RetrievalService.MaxTopK)]
    public async Task Top_k_is_kept_within_what_retrieval_accepts(int asked, int expected)
    {
        var retrieval = new Retrieval(() => []);

        await Service(retrieval).SearchAsync("kira", asked);

        Assert.Equal(expected, retrieval.AskedTopK);
    }
}
