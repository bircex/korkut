using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Korkut.Ocr.Tests.Integration;

namespace Korkut.Ocr.Tests;

/// <summary>
/// The OCR host: /extract turns uploaded document bytes into text via the engine, guarded by
/// matters.read, and reports 503 when no engine is configured (the in-code scaffold).
/// </summary>
public class OcrEndpointTests : IClassFixture<KorkutOcrFactory>, IClassFixture<DisabledOcrFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly KorkutOcrFactory _withEngine;
    private readonly DisabledOcrFactory _noEngine;

    public OcrEndpointTests(KorkutOcrFactory withEngine, DisabledOcrFactory noEngine)
    {
        _withEngine = withEngine;
        _noEngine = noEngine;
    }

    private static MultipartFormDataContent File(string name, string content, string contentType = "image/png")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "file", name);
        return form;
    }

    [Fact]
    public async Task Extract_returns_the_engines_text()
    {
        var res = await _withEngine.AuthedClient().PostAsync("/extract", File("tarama.png", "Tahliye ihtarnamesi metni"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains("Tahliye ihtarnamesi metni", body.GetProperty("text").GetString());
        Assert.Equal(1, body.GetProperty("pages").GetInt32());
    }

    [Fact]
    public async Task Extract_returns_the_per_page_split_as_page_texts()
    {
        // The caller (HttpOcrService, behind the extractor's OCR fallback) builds page-numbered
        // spans from this field, so both its snake_case name and its shape are contract.
        var res = await _withEngine.AuthedClient().PostAsync("/extract", File("tarama.png", "Sayfa metni"));
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);

        var pageTexts = body.GetProperty("page_texts");
        Assert.Equal(JsonValueKind.Array, pageTexts.ValueKind);
        Assert.Contains("Sayfa metni", pageTexts[0].GetString());
    }

    [Fact]
    public async Task Extract_without_a_file_is_rejected()
    {
        var form = new MultipartFormDataContent { { new StringContent("hi"), "note" } };
        var res = await _withEngine.AuthedClient().PostAsync("/extract", form);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var res = await _withEngine.CreateClient().PostAsync("/extract", File("x.png", "data"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Extract_is_503_when_no_engine_is_configured()
    {
        var res = await _noEngine.AuthedClient().PostAsync("/extract", File("x.png", "data"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
    }

    [Fact]
    public async Task Health_is_served()
    {
        var res = await _withEngine.CreateClient().GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
