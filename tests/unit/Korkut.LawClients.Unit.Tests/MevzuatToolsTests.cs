using System.Text;
using System.Text.Json;
using Korkut.Core;
using Korkut.LawClients.Services;
using Korkut.LawClients.Services.Sources;
using Korkut.Tools.Core;
using Korkut.Tools.Law;
using Microsoft.Extensions.Caching.Memory;

namespace Korkut.LawClients.Unit.Tests;

/// <summary>
/// The mevzuat tools against a scripted Bedesten API: what each tool argument turns into on the
/// wire (scope routing, number/date/gazette filters, MADDE fetches) and how responses — including
/// the API's own failure envelope — come back to the model. No network; the handler replays canned
/// payloads shaped like real bedesten.adalet.gov.tr responses. The tools themselves (SearchDecisionsTool
/// etc.) are the public Korkut.Tools adapters; this exercises them end-to-end against a real
/// LawClientsService wired only to the fake transport.
/// </summary>
public class MevzuatToolsTests
{
    private static readonly AgentToolContext Ctx =
        new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string> { Permissions.DocumentsRead });

    private sealed class NoEnv : IEnvironment
    {
        public string? Get(string name) => null;
    }

    /// <summary>Scripted transport: replays a canned body per path and records every request.</summary>
    private sealed class FakeBedesten : HttpMessageHandler, IHttpClientFactory
    {
        private readonly Dictionary<string, string> _responses = new(StringComparer.OrdinalIgnoreCase);

        public List<(string Path, JsonDocument Body)> Requests { get; } = [];

        public FakeBedesten On(string path, string responseJson)
        {
            _responses[path] = responseJson;
            return this;
        }

        public HttpClient CreateClient(string name)
            => new(this, disposeHandler: false) { BaseAddress = new Uri("https://bedesten.test") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = await request.Content!.ReadAsStringAsync(ct);
            Requests.Add((path, JsonDocument.Parse(body)));
            return _responses.TryGetValue(path, out var canned)
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(canned, Encoding.UTF8, "application/json"),
                }
                : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }
    }

    /// <summary>The real LawClientsService (search + document reads), wired only to the fake transport.</summary>
    private static LawClientsService Service(FakeBedesten api) =>
        new([new MevzuatClient(api)], new NoEnv(), new MemoryCache(new MemoryCacheOptions()));

    private static JsonElement Args(string json) => ToolSchema.Parse(json);

    private static string Success(string dataJson) =>
        """{"data":""" + dataJson + ""","metadata":{"FMTY":"SUCCESS"}}""";

    private static string B64Html(string html) => Convert.ToBase64String(Encoding.UTF8.GetBytes(html));

    private static JsonElement LastRequestData(FakeBedesten api, string path)
        => api.Requests.Last(r => r.Path == path).Body.RootElement.GetProperty("data");

    private const string SearchPath = "/mevzuat/searchDocuments";

    private const string DocumentPath = "/mevzuat/getDocumentContent";

    private const string EmptySearch = """{"data":{"mevzuatList":[],"total":0},"metadata":{"FMTY":"SUCCESS"}}""";

    // ---- search_decisions (source=mevzuat) ------------------------------------------------------

    [Fact]
    public async Task Digit_only_query_routes_to_the_number_filter_and_results_carry_gazette_and_gerekce()
    {
        var api = new FakeBedesten().On(SearchPath, Success(
            """
            {"mevzuatList":[{"mevzuatId":"103228","mevzuatNo":5237,"mevzuatAdi":"TÜRK CEZA KANUNU",
              "mevzuatTur":{"id":1,"name":"KANUN"},"resmiGazeteSayisi":"25611","gerekceId":"1563"}],"total":1}
            """));
        var tool = new SearchDecisionsTool(Service(api));

        var result = await tool.InvokeAsync(Args("""{"source":"mevzuat","query":"5237"}"""), Ctx);

        Assert.False(result.IsError);
        var sent = LastRequestData(api, SearchPath);
        Assert.Equal("5237", sent.GetProperty("mevzuatNo").GetString());
        Assert.False(sent.TryGetProperty("mevzuatAdi", out _)); // number never hits the title index
        Assert.Contains("\"gazette_number\":\"25611\"", result.Content);
        Assert.Contains("\"gerekce_id\":\"1563\"", result.Content);
    }

    [Theory]
    [InlineData("content", "phrase", null)]
    [InlineData("title_and_content", "mevzuatAdi", "basliktaAra")]
    [InlineData("title", "mevzuatAdi", null)]
    public async Task Search_scope_routes_the_query_to_the_matching_api_field(
        string scope, string expectedField, string? expectedFlag)
    {
        var api = new FakeBedesten().On(SearchPath, EmptySearch);
        var tool = new SearchDecisionsTool(Service(api));

        await tool.InvokeAsync(
            Args($$"""{"source":"mevzuat","query":"ticaret","search_scope":"{{scope}}"}"""), Ctx);

        var sent = LastRequestData(api, SearchPath);
        Assert.Equal("ticaret", sent.GetProperty(expectedField).GetString());
        if (expectedFlag is not null)
        {
            Assert.False(sent.GetProperty(expectedFlag).GetBoolean());
        }
        else
        {
            Assert.False(sent.TryGetProperty("basliktaAra", out _));
        }
    }

    [Fact]
    public async Task Filters_map_to_gazette_fields_with_turkey_midnight_conversion()
    {
        var api = new FakeBedesten().On(SearchPath, EmptySearch);
        var tool = new SearchDecisionsTool(Service(api));

        await tool.InvokeAsync(Args(
            """
            {"source":"mevzuat","query":"vergi","tam_cumle":true,"legislation_type":"KANUN,TEBLIGLER",
             "date_start":"2026-01-01","date_end":"2026-07-01","gazette_number":"33300"}
            """), Ctx);

        var sent = LastRequestData(api, SearchPath);
        Assert.True(sent.GetProperty("tamCumle").GetBoolean());
        Assert.Equal(["KANUN", "TEBLIGLER"], sent.GetProperty("mevzuatTurList").EnumerateArray().Select(e => e.GetString()));
        // Turkey is UTC+3: local midnight of the 1st is the previous day 21:00 UTC.
        Assert.Equal("2025-12-31T21:00:00.000Z", sent.GetProperty("resmiGazeteTarihiStart").GetString());
        Assert.Equal("2026-07-01T21:00:00.000Z", sent.GetProperty("resmiGazeteTarihiEnd").GetString());
        Assert.Equal("33300", sent.GetProperty("resmiGazeteSayisi").GetString());
    }

    [Fact]
    public async Task The_apis_failure_envelope_surfaces_as_a_tool_error_with_its_message()
    {
        var api = new FakeBedesten().On(SearchPath,
            """{"data":null,"metadata":{"FMTY":"ERROR","FMTE":"Geçersiz istek"}}""");
        var tool = new SearchDecisionsTool(Service(api));

        var result = await tool.InvokeAsync(Args("""{"source":"mevzuat","query":"x"}"""), Ctx);

        Assert.True(result.IsError);
        Assert.Contains("Geçersiz istek", result.Content);
    }

    // ---- fetch_document (source=mevzuat) --------------------------------------------------------

    [Fact]
    public async Task Fetch_with_article_id_requests_a_MADDE_document()
    {
        var api = new FakeBedesten().On(DocumentPath, Success(
            $$"""{"content":"{{B64Html("<p>Madde 1- Amaç hükmü.</p>")}}","mimeType":"text/html","maddeBaslik":"Amaç"}"""));
        var tool = new FetchDocumentTool(Service(api));

        var result = await tool.InvokeAsync(
            Args("""{"source":"mevzuat","id":"103228","article_id":"1272772"}"""), Ctx);

        Assert.False(result.IsError);
        var sent = LastRequestData(api, DocumentPath);
        Assert.Equal("MADDE", sent.GetProperty("documentType").GetString());
        Assert.Equal("1272772", sent.GetProperty("id").GetString());
        Assert.Contains("Amaç hükmü", result.Content);
    }

    // ---- search_within_mevzuat ------------------------------------------------------------------

    [Fact]
    public async Task Within_search_returns_matching_articles_ranked_by_score()
    {
        var html = "<p>MADDE 1 – Bu Kanunun amacı vergi düzenidir.</p>" +
                   "<p>MADDE 2 – Vergi mükellefi, vergi dairesine vergi beyan eder.</p>" +
                   "<p>MADDE 3 – Ceza hükümleri saklıdır.</p>";
        var api = new FakeBedesten().On(DocumentPath, Success(
            $$"""{"content":"{{B64Html(html)}}","mimeType":"text/html"}"""));
        var tool = new SearchWithinMevzuatTool(Service(api));

        var result = await tool.InvokeAsync(
            Args("""{"mevzuat_id":"103228","keyword":"vergi"}"""), Ctx);

        Assert.False(result.IsError);
        using var body = JsonDocument.Parse(result.Content);
        var root = body.RootElement;
        Assert.Equal(3, root.GetProperty("total_articles").GetInt32());
        Assert.Equal(2, root.GetProperty("matching_articles").GetInt32());
        var results = root.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal("2", results[0].GetProperty("madde_no").GetString()); // 3 hits outrank 1
        Assert.Equal(3, results[0].GetProperty("match_count").GetInt32());
        Assert.Equal("1", results[1].GetProperty("madde_no").GetString());
    }

    [Fact]
    public async Task Within_search_on_an_unstructured_document_points_to_fetch_document()
    {
        var api = new FakeBedesten().On(DocumentPath, Success(
            $$"""{"content":"{{B64Html("<p>Serbest metin, madde başlığı yok.</p>")}}","mimeType":"text/html"}"""));
        var tool = new SearchWithinMevzuatTool(Service(api));

        var result = await tool.InvokeAsync(Args("""{"mevzuat_id":"1","keyword":"metin"}"""), Ctx);

        Assert.True(result.IsError);
        Assert.Contains("fetch_document", result.Content);
    }

    // ---- get_mevzuat_madde_tree -----------------------------------------------------------------

    [Fact]
    public async Task Madde_tree_renders_the_outline_with_fetchable_ids()
    {
        var api = new FakeBedesten().On("/mevzuat/mevzuatMaddeTree", Success(
            """
            {"children":[{"maddeId":"9","title":"BİRİNCİ KISIM","children":[
              {"maddeId":"1272772","maddeNo":1,"title":"Amaç","gerekceId":1536,"children":[]}]}]}
            """));
        var tool = new GetMevzuatMaddeTreeTool(Service(api));

        var result = await tool.InvokeAsync(Args("""{"mevzuat_id":"103228"}"""), Ctx);

        Assert.False(result.IsError);
        Assert.Contains("BİRİNCİ KISIM", result.Content);
        Assert.Contains("MADDE 1: Amaç [madde_id=1272772, gerekce_id=1536]", result.Content);
    }

    [Fact]
    public async Task An_empty_madde_tree_is_a_friendly_note_not_an_error()
    {
        var api = new FakeBedesten().On("/mevzuat/mevzuatMaddeTree", Success("""{"children":[]}"""));
        var tool = new GetMevzuatMaddeTreeTool(Service(api));

        var result = await tool.InvokeAsync(Args("""{"mevzuat_id":"42"}"""), Ctx);

        Assert.False(result.IsError); // circulars legitimately have no tree
        Assert.Contains("search_within_mevzuat", result.Content);
    }

    // ---- get_mevzuat_gerekce --------------------------------------------------------------------

    [Fact]
    public async Task Gerekce_returns_readable_text_with_owner_and_paging_header()
    {
        var api = new FakeBedesten().On("/mevzuat/getGerekceContent", Success(
            $$"""{"gerekceId":"1563","mevzuatId":"103228","content":"{{B64Html("<p>GENEL GEREKÇE</p><p>Kanunun amacı şudur.</p>")}}","mimeType":"text/html"}"""));
        var tool = new GetMevzuatGerekceTool(Service(api));

        var result = await tool.InvokeAsync(Args("""{"gerekce_id":"1563"}"""), Ctx);

        Assert.False(result.IsError);
        Assert.Contains("Gerekçe 1563 (mevzuat 103228), sayfa 1/1", result.Content);
        Assert.Contains("GENEL GEREKÇE", result.Content);
        Assert.Contains("Kanunun amacı şudur.", result.Content);
        Assert.DoesNotContain("<p>", result.Content); // HTML is reduced to text
    }

    [Fact]
    public async Task A_gerekce_without_published_content_is_an_error()
    {
        var api = new FakeBedesten().On("/mevzuat/getGerekceContent", Success(
            """{"gerekceId":"9999","content":""}"""));
        var tool = new GetMevzuatGerekceTool(Service(api));

        var result = await tool.InvokeAsync(Args("""{"gerekce_id":"9999"}"""), Ctx);

        Assert.True(result.IsError);
        Assert.Contains("9999", result.Content);
    }
}
