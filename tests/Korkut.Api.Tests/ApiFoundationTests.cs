using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Korkut.Api.Tests;

/// <summary>
/// Pins the API foundation behavior to the Python parity contract: snake_case
/// JSON, the { items, total } paged envelope, the { detail } error body, and the
/// { detail: [ { type, loc, msg, input } ] } validation body.
/// </summary>
public class ApiFoundationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient Client => factory.CreateClient();

    [Fact]
    public async Task Config_uses_snake_case_keys()
    {
        var json = await Client.GetStringAsync("/api/v1/config");
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("demo_enabled", out _));
        Assert.True(doc.RootElement.TryGetProperty("google_configured", out _));
        Assert.False(doc.RootElement.TryGetProperty("demoEnabled", out _), "must not be camelCase");
    }

    [Fact]
    public async Task List_uses_items_total_envelope()
    {
        var json = await Client.GetStringAsync("/api/v1/samples");
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("items").ValueKind);
        Assert.Equal(2, doc.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Valid_echo_returns_ok()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/echo", new { title = "Dava dilekçesi" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Dava dilekçesi", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Invalid_echo_returns_422_with_detail_list()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/echo", new { title = (string?)null });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var detail = body.GetProperty("detail");
        Assert.Equal(JsonValueKind.Array, detail.ValueKind);
        var first = detail[0];
        Assert.True(first.TryGetProperty("type", out _));
        Assert.True(first.TryGetProperty("loc", out var loc));
        Assert.Equal("body", loc[0].GetString());
        Assert.True(first.TryGetProperty("msg", out _));
    }

    [Fact]
    public async Task Api_error_returns_detail_string()
    {
        var response = await Client.GetAsync("/api/v1/not-found-demo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Kayıt bulunamadı", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Response_carries_correlation_id_header()
    {
        var response = await Client.GetAsync("/api/v1/config");

        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }
}
