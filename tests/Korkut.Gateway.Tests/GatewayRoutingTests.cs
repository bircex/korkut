using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Gateway.Tests.Integration;

namespace Korkut.Gateway.Tests;

/// <summary>
/// The YARP gateway over real stub upstreams: path/method routing to the Agent vs API cluster, and
/// the edge auth gate (anonymous rejected on the protected agent surface, operational API routes
/// passed through for the API to enforce).
/// </summary>
public class GatewayRoutingTests(KorkutGatewayFactory factory) : IClassFixture<KorkutGatewayFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<string> UpstreamOf(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("upstream").GetString()!;
    }

    [Fact]
    public async Task Anonymous_agent_route_is_rejected_at_the_edge()
    {
        var res = await factory.CreateClient().GetAsync("/api/sessions");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Authenticated_agent_route_is_routed_to_the_agent_host()
    {
        var res = await factory.AuthedClient().GetAsync("/api/sessions/00000000-0000-0000-0000-000000000000");
        Assert.Equal("agent", await UpstreamOf(res));
    }

    [Fact]
    public async Task Operational_api_route_passes_through_the_edge_to_the_api_host()
    {
        // No token: the gateway does NOT gate operational routes — the API enforces its own auth.
        var res = await factory.CreateClient().GetAsync("/api/clients");
        Assert.Equal("api", await UpstreamOf(res));
    }

    [Fact]
    public async Task Document_routes_split_by_method_between_agent_and_api()
    {
        var authed = factory.AuthedClient();
        var docId = Guid.NewGuid();

        // PUT (save a draft) is an agent action; the drafting sub-actions too.
        Assert.Equal("agent", await UpstreamOf(await authed.PutAsJsonAsync($"/api/documents/{docId}", new { title = "x", content_md = "y" }, Json)));
        Assert.Equal("agent", await UpstreamOf(await authed.PostAsync($"/api/documents/{docId}/draft", null)));

        // GET a document (download) stays operational → API cluster.
        Assert.Equal("api", await UpstreamOf(await factory.CreateClient().GetAsync($"/api/documents/{docId}")));
    }

    [Fact]
    public async Task The_forwarded_request_carries_the_caller_token_downstream()
    {
        var res = await factory.AuthedClient().GetAsync("/api/sessions/00000000-0000-0000-0000-000000000000");
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(body.GetProperty("authorized").GetBoolean());
    }

    [Fact]
    public async Task Health_is_served_by_the_gateway_itself()
    {
        var res = await factory.CreateClient().GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
