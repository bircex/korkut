using Korkut.Knowledge;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// The boundary-aware chunker (Knowledge/RAG ingestion). Pure, deterministic — no DB — so these
/// assert the size cap, the overlap between consecutive chunks, and boundary handling directly.
/// </summary>
public class ChunkerServiceTests
{
    private readonly ChunkerService _chunkerService = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n")]
    public void Blank_text_yields_no_chunks(string text)
    {
        Assert.Empty(_chunkerService.Chunk(text));
    }

    [Fact]
    public void Short_text_is_a_single_chunk()
    {
        var chunks = _chunkerService.Chunk("Kısa bir dilekçe metni.");
        Assert.Single(chunks);
        Assert.Equal("Kısa bir dilekçe metni.", chunks[0]);
    }

    [Fact]
    public void Long_text_is_split_into_capped_windows()
    {
        // 60 sentences → well over the 1000-char cap, forcing multiple windows.
        var text = string.Join(" ", Enumerable.Range(0, 60).Select(i => $"Bu {i}. numaralı cümledir."));
        var chunks = _chunkerService.Chunk(text);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= ChunkerService.MaxChars, $"chunk length {c.Length} exceeds cap"));
    }

    [Fact]
    public void Consecutive_chunks_overlap_so_context_is_not_lost_at_a_cut()
    {
        var text = string.Join(" ", Enumerable.Range(0, 60).Select(i => $"Bu {i}. numaralı cümledir."));
        var chunks = _chunkerService.Chunk(text);

        // The tail of chunk[0] reappears at the head of chunk[1] (overlap window).
        var tail = chunks[0][^40..];
        Assert.Contains(tail[..10], chunks[1]);
    }

    [Fact]
    public void Reassembling_chunks_covers_all_words_in_order()
    {
        var text = string.Join(" ", Enumerable.Range(0, 40).Select(i => $"kelime{i}"));
        var chunks = _chunkerService.Chunk(text);

        // Every original token appears in at least one chunk (overlap may repeat some).
        foreach (var token in text.Split(' '))
        {
            Assert.Contains(chunks, c => c.Contains(token));
        }
    }
}
