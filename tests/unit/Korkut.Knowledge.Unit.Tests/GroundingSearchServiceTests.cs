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

    private static GroundingSearchService Service(IRetrievalService retrieval)
        => new(retrieval, NullLogger<GroundingSearchService>.Instance);

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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(retrieval).SearchAsync("kira", 3, cts.Token));
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
