using Korkut.LawClients.Services.Shared;

namespace Korkut.LawClients.Unit.Tests;

/// <summary>
/// The article splitter and boolean matcher behind <c>search_within_mevzuat</c> are pure text
/// logic ported from the Python <c>article_search.py</c>; these pin the header shapes real
/// legislation uses and the operator semantics the tool schema promises.
/// </summary>
public class MaddeSearchTests
{
    [Fact]
    public void Splits_standard_dash_headers()
    {
        var text = "TÜRK CEZA KANUNU\nMADDE 1 – Ceza Kanununun amacı esastır.\nMADDE 2 – Suçta kanunîlik ilkesi geçerlidir.";

        var articles = MaddeSearch.Split(text);

        Assert.Equal(2, articles.Count);
        Assert.Equal("1", articles[0].No);
        Assert.Contains("amacı", articles[0].Content);
        Assert.Equal("2", articles[1].No);
        Assert.DoesNotContain("amacı", articles[1].Content);
    }

    [Theory]
    [InlineData("Madde 328. Devletin güvenliğine ilişkin bilgiler.", "328")] // title case + dot (TCK style)
    [InlineData("MADDE 5/A – Ek fıkra hükmü.", "5/A")]                       // slash-suffixed article
    [InlineData("**MADDE 7 –** Yürürlük.", "7")]                             // markdown bold
    [InlineData("MADDE 464­- Saklı hükümler.", "464")]                  // soft hyphen dash
    public void Splits_header_variants(string text, string expectedNo)
    {
        var articles = MaddeSearch.Split(text);

        Assert.Single(articles);
        Assert.Equal(expectedNo, articles[0].No);
    }

    [Fact]
    public void Normalizes_prefixed_articles()
    {
        var text = "EK MADDE 8 – Ek hüküm.\nGEÇİCİ MADDE 3 – Geçiş hükmü.\nMÜKERRER MADDE 5 – Tekrar hükmü.";

        var articles = MaddeSearch.Split(text);

        Assert.Equal(["Ek 8", "Geçici 3", "Mükerrer 5"], articles.Select(a => a.No));
    }

    [Fact]
    public void Split_returns_empty_when_no_headers()
    {
        Assert.Empty(MaddeSearch.Split("Bu metinde hiç madde başlığı yok; sadece 5 inci maddesine atıf var."));
    }

    [Fact]
    public void And_requires_both_terms_and_sums_occurrences()
    {
        var content = "Yatırımcı haklarını korur; yatırımcı tazmin merkezine başvurur.";

        var (match, score) = MaddeSearch.Matches(content, "yatırımcı AND tazmin");
        Assert.True(match);
        Assert.Equal(3, score); // yatırımcı ×2 + tazmin ×1

        (match, _) = MaddeSearch.Matches(content, "yatırımcı AND kurul");
        Assert.False(match);
    }

    [Fact]
    public void Or_matches_when_first_term_is_absent()
    {
        // The first (implicit AND) term missing must not eliminate the article before OR runs.
        var (match, score) = MaddeSearch.Matches("Sadece müşteri kaydı bulunur.", "yatırımcı OR müşteri");

        Assert.True(match);
        Assert.Equal(1, score);
    }

    [Fact]
    public void Not_eliminates_articles_containing_the_term()
    {
        var (match, _) = MaddeSearch.Matches("Yatırımcı ve kurum birlikte anılır.", "yatırımcı NOT kurum");
        Assert.False(match);

        (match, _) = MaddeSearch.Matches("Yalnızca yatırımcı anılır.", "yatırımcı NOT kurum");
        Assert.True(match);
    }

    [Fact]
    public void Quoted_phrase_is_required_and_scores_double()
    {
        var content = "Şirketin mali sıkıntı içinde olması hâlinde mali sıkıntı tespiti yapılır.";

        var (match, score) = MaddeSearch.Matches(content, "\"mali sıkıntı\"");
        Assert.True(match);
        Assert.Equal(4, score); // 2 occurrences × 2

        (match, _) = MaddeSearch.Matches(content, "\"mali kriz\"");
        Assert.False(match);
    }

    [Fact]
    public void Matching_folds_turkish_capitals()
    {
        // İ→i and I→ı, the folds InvariantCulture gets wrong.
        var (match, _) = MaddeSearch.Matches("SİGORTA ŞIRKETİ SORUMLUDUR.", "sigorta AND şırketi");

        Assert.True(match);
    }

    [Fact]
    public void Case_sensitive_mode_respects_case()
    {
        var (match, _) = MaddeSearch.Matches("Yatırımcı korunur.", "yatırımcı", caseSensitive: true);
        Assert.False(match);

        (match, _) = MaddeSearch.Matches("Yatırımcı korunur.", "Yatırımcı", caseSensitive: true);
        Assert.True(match);
    }

    [Fact]
    public void Preview_centers_on_the_first_term_with_ellipses()
    {
        var content = new string('a', 300) + " tazmin " + new string('b', 300);

        var preview = MaddeSearch.Preview(content, "tazmin AND yok");

        Assert.StartsWith("...", preview);
        Assert.EndsWith("...", preview);
        Assert.Contains("tazmin", preview);
        Assert.True(preview.Length < 250);
    }

    // The madde-tree JSON→outline rendering used to be tested here directly against
    // GetMevzuatMaddeTreeTool.Format(JsonElement); that step now lives split across
    // LawClientsService (JSON → typed LawClientsMaddeTreeNode) and the tool (typed tree → outline
    // text) — covered end-to-end by MevzuatToolsTests.Madde_tree_renders_the_outline_with_fetchable_ids.
}
