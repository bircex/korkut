using Korkut.Agent.Tools.Law;
using Korkut.Application.Runtime;

namespace Korkut.Agent.Tests;

/// <summary>
/// Pure helpers behind the legal-source clients: the dependency-free HTML→text reduction, Markdown
/// paging, the Sigorta Tahkim journal helpers (issue→filename, decision split, Turkish lowercase),
/// and Tavily's no-key short-circuit. The concrete portal clients hit live government hosts and are
/// exercised out of band.
/// </summary>
public class LawHelpersTests
{
    [Fact]
    public void HtmlToMarkdown_strips_tags_and_decodes_entities()
    {
        var md = LegalText.HtmlToMarkdown("<p>Karar&nbsp;metni</p><br><div>ikinci&amp;son</div>");

        Assert.DoesNotContain("<", md);
        Assert.Contains("Karar", md);
        Assert.Contains("ikinci&son", md);
    }

    [Fact]
    public void ChunkMarkdown_paginates_and_clamps()
    {
        var text = new string('a', 12000);

        var (chunk, page, totalPages) = LegalText.ChunkMarkdown(text, page: 2, pageSize: 5000);

        Assert.Equal(3, totalPages);
        Assert.Equal(2, page);
        Assert.Equal(5000, chunk.Length);

        var (_, clampedPage, _) = LegalText.ChunkMarkdown(text, page: 99, pageSize: 5000);
        Assert.Equal(3, clampedPage);
    }

    [Fact]
    public void ExtractDivHtml_returns_inner_html_balancing_nested_divs()
    {
        const string html = "<html><body><div class=\"x blog-post-inner\">önce<div>iç</div>son</div><div>dış</div></body></html>";

        var inner = LegalText.ExtractDivHtml(html, "blog-post-inner");

        Assert.Equal("önce<div>iç</div>son", inner);
    }

    [Fact]
    public void ExtractDivHtml_falls_back_to_whole_document_when_missing()
    {
        const string html = "<div class=\"other\">x</div>";
        Assert.Equal(html, LegalText.ExtractDivHtml(html, "blog-post-inner"));
    }

    [Theory]
    [InlineData(4, "karardergisisayi4.pdf")]
    [InlineData(59, "revizekd59.pdf")]
    [InlineData(120, "karardrgs120.pdf")]
    public void SigortaTahkim_PdfFilename_maps_issue_numbers(int issue, string expected)
        => Assert.Equal(expected, SigortaTahkim.PdfFilename(issue));

    [Fact]
    public void SigortaTahkim_TurkishLower_handles_dotted_and_dotless_i()
    {
        Assert.Equal("işıl", SigortaTahkim.TurkishLower("İŞIL"));
    }

    [Fact]
    public void SigortaTahkim_SplitIntoDecisions_splits_on_header_and_drops_short_bodies()
    {
        var longBody = new string('x', 1200);
        var text =
            "İçindekiler\n" +
            "01.02.2024 Tarih ve K-2024/123 Sayılı Hakem Kararı\n" + longBody + "\n" +
            "05.03.2024 Tarih ve K-2024/9 Sayılı Hakem Kararı\nkısa gövde";

        var decisions = SigortaTahkim.SplitIntoDecisions(text);

        Assert.Single(decisions);
        Assert.Contains("K-2024/123", decisions[0].Header);
        Assert.True(decisions[0].Body.Length >= 1000);
    }

    [Fact]
    public async Task TavilyClient_returns_empty_without_a_key()
    {
        var tavily = new TavilyClient(new ThrowingHttpClientFactory(), new FakeEnv([]));

        var hits = await tavily.SearchAsync("x", ["kvkk.gov.tr"], 10);

        Assert.Empty(hits);
    }

    private sealed class FakeEnv(Dictionary<string, string> vars) : IEnvironment
    {
        public string? Get(string name) => vars.GetValueOrDefault(name);
    }

    private sealed class ThrowingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("must not create a client without a key");
    }
}
