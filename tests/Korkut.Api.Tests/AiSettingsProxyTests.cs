using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Korkut.Api.Controllers;
using Korkut.Core;
using Korkut.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Api.Tests;

/// <summary>
/// The AI settings screen reaches the model gateway's management API through the app, for the
/// owner only, with the gateway's token added on the server. A stand-in gateway records what
/// arrives, so the tests pin what is forwarded and what never is.
/// </summary>
public class AiSettingsProxyTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class Gateway : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url, string? Auth, string? Body)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Seen.Add((request.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), body));
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"id\":\"anthropic\",\"credential\":\"…a1b2\"}", Encoding.UTF8, "application/json"),
            };
        }
    }

    private (HttpClient Client, Gateway Gateway) Host(string role, string? llmrUrl = "http://llmr.test:8080")
    {
        var gateway = new Gateway();
        var app = factory.WithWebHostBuilder(b =>
        {
            if (llmrUrl is not null)
            {
                b.UseSetting("KORKUT_LLMR_URL", llmrUrl);
                b.UseSetting("KORKUT_LLMR_TOKEN", "gateway-token");
            }

            b.ConfigureTestServices(s => s.AddHttpClient(AiSettingsController.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => gateway));
        });
        var client = app.CreateClient();
        if (role != "anonymous")
        {
            var token = new AccessTokenService(Environment.GetEnvironmentVariable("KORKUT_SECRET")!, SystemClock.Instance)
                .Mint(new AccessClaims(Guid.NewGuid(), Guid.NewGuid(), role, "s1"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return (client, gateway);
    }

    [Fact]
    public async Task The_owner_reaches_the_gateway_with_its_token_never_their_own()
    {
        var (client, gateway) = Host(Roles.OfficeOwner);

        var response = await client.PostAsync("/api/settings/ai/manage/providers",
            new StringContent("{\"id\":\"anthropic\",\"type\":\"anthropic\",\"credential\":\"sk-ant-test\"}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("…a1b2", await response.Content.ReadAsStringAsync());
        var (method, url, auth, body) = Assert.Single(gateway.Seen);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("http://llmr.test:8080/manage/providers", url);
        Assert.Equal("Bearer gateway-token", auth);
        Assert.Contains("sk-ant-test", body);
    }

    [Fact]
    public async Task Paths_and_queries_pass_through_unchanged()
    {
        var (client, gateway) = Host(Roles.OfficeOwner);

        await client.PutAsync("/api/settings/ai/manage/providers/openrouter/models/meta-llama%2Fllama-3.1-70b",
            new StringContent("{\"enabled\":true}", Encoding.UTF8, "application/json"));
        await client.GetAsync("/api/settings/ai/manage/usage?group_by=model&from=1790380800");

        Assert.Equal("http://llmr.test:8080/manage/providers/openrouter/models/meta-llama%2Fllama-3.1-70b", gateway.Seen[0].Url);
        Assert.Equal("http://llmr.test:8080/manage/usage?group_by=model&from=1790380800", gateway.Seen[1].Url);
    }

    [Theory]
    [InlineData("/api/settings/ai/manage/../../v1/chat/completions")]
    [InlineData("/api/settings/ai/manage/providers/%2e%2e/%2e%2e/healthz")]
    [InlineData("/api/settings/ai/manage/something-else")]
    [InlineData("/api/settings/ai/manage")]
    public async Task Nothing_outside_the_screens_part_of_the_api_is_forwarded(string path)
    {
        var (client, gateway) = Host(Roles.OfficeOwner);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(gateway.Seen);
    }

    [Fact]
    public async Task A_member_who_is_not_the_owner_is_refused()
    {
        var (client, gateway) = Host(Roles.Lawyer);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/settings/ai/manage/status")).StatusCode);
        Assert.Empty(gateway.Seen);
    }

    [Fact]
    public async Task Anonymous_is_refused()
    {
        var (client, gateway) = Host("anonymous");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/settings/ai/manage/status")).StatusCode);
        Assert.Empty(gateway.Seen);
    }

    [Fact]
    public async Task Without_a_gateway_the_screen_is_told_so()
    {
        var (client, gateway) = Host(Roles.OfficeOwner, llmrUrl: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/settings/ai/manage/status")).StatusCode);
        Assert.Empty(gateway.Seen);
    }
}
