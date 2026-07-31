using Korkut.Agent.Chat.Services;
using Korkut.Contract.Agent.Chat;

namespace Korkut.Agent.Chat.Unit.Tests;

/// <summary>
/// The per-message sources pipeline: normalization (dedup by source+id, hard cap, snippet trim)
/// and the snake_case JSON that goes into chat_messages.sources_json — the same shape the API
/// serves, so a persisted list must round-trip unchanged.
/// </summary>
public class MessageSourcesTests
{
    private static SourceRef Karar(string id, string source = "yargitay", string? snippet = null) =>
        new("ictihat", source, id, $"Karar {id}", Date: "2020-01-01", Snippet: snippet);

    [Fact]
    public void Normalize_returns_null_for_empty_input()
    {
        Assert.Null(MessageSources.Normalize(null));
        Assert.Null(MessageSources.Normalize([]));
    }

    [Fact]
    public void Normalize_dedupes_by_source_and_id_keeping_first_hit_and_order()
    {
        var normalized = MessageSources.Normalize(
        [
            Karar("2015/89"),
            Karar("2013/24590"),
            Karar("2015/89", source: "YARGITAY"), // aynı karar, kaynak adı farklı harfle
            Karar("2015/89", source: "danistay"), // farklı kaynak → ayrı kayıt
        ]);

        Assert.NotNull(normalized);
        Assert.Equal(3, normalized!.Count);
        Assert.Equal(["2015/89", "2013/24590", "2015/89"], normalized.Select(s => s.Id));
        Assert.Equal("yargitay", normalized[0].Source); // ilk gelen kazanır
    }

    [Fact]
    public void Normalize_caps_the_list_and_trims_long_snippets()
    {
        var many = Enumerable.Range(1, MessageSources.MaxPerMessage + 10)
            .Select(i => Karar($"2020/{i}", snippet: new string('a', MessageSources.MaxSnippetChars + 50)))
            .ToList();

        var normalized = MessageSources.Normalize(many);

        Assert.Equal(MessageSources.MaxPerMessage, normalized!.Count);
        Assert.All(normalized, s =>
        {
            Assert.NotNull(s.Snippet);
            Assert.True(s.Snippet!.Length <= MessageSources.MaxSnippetChars + 1); // +1: "…"
            Assert.EndsWith("…", s.Snippet);
        });
    }

    [Fact]
    public void Serialize_uses_snake_case_and_omits_nulls()
    {
        var json = MessageSources.Serialize(
            [new SourceRef("ictihat", "yargitay", "id-1", "Başlık", CaseNumber: "2015/89")]);

        Assert.NotNull(json);
        Assert.Contains("\"case_number\":\"2015/89\"", json);
        Assert.DoesNotContain("decision_number", json); // null alanlar yazılmaz
    }

    [Fact]
    public void Serialized_sources_round_trip_unchanged()
    {
        var original = new List<SourceRef>
        {
            new("ictihat", "yargitay", "id-1", "Başlık", "2018-04-02", "2015/89", "2018/140",
                "Yargıtay", "Ceza Genel Kurulu", "https://example.org", "özet"),
            new("mevzuat", "mevzuat", "5237", "Türk Ceza Kanunu"),
        };

        var back = MessageSources.Deserialize(MessageSources.Serialize(original));

        Assert.Equal(original, back);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bozuk json {{")]
    [InlineData("[]")]
    public void Deserialize_tolerates_missing_or_corrupt_payloads(string? json)
    {
        Assert.Null(MessageSources.Deserialize(json));
    }
}
