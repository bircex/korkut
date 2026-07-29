using Korkut.TestSupport;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;

namespace Korkut.Api.Tests;

/// <summary>
/// Correspondence register ("evrak") over the real API + real PostgreSQL with RLS
/// (endpoint migration, slice 11). CRUD, the direction filter, tenant isolation.
/// The API speaks snake_case JSON, so requests are serialized and responses read
/// with the snake_case naming policy.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class EvrakEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task Create_list_filter_and_delete()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var inc = await (await client.PostAsJsonAsync("/api/evrak",
            new { direction = "incoming", title = "Tebligat", referenceNo = "2026/123", docDate = "2026-07-01" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var incId = inc.GetProperty("id").GetGuid();
        Assert.Equal("incoming", inc.GetProperty("direction").GetString());
        Assert.Equal("2026/123", inc.GetProperty("reference_no").GetString());
        Assert.Equal("2026-07-01", inc.GetProperty("doc_date").GetString());

        await client.PostAsJsonAsync("/api/evrak", new { direction = "outgoing", title = "Dilekçe" }, Json);

        // Unfiltered list has both; direction filter narrows to one.
        var all = await client.GetFromJsonAsync<List<JsonElement>>("/api/evrak", Json);
        Assert.Equal(2, all!.Count);
        var incoming = await client.GetFromJsonAsync<List<JsonElement>>("/api/evrak?direction=incoming", Json);
        Assert.Single(incoming!);
        Assert.Equal(incId, incoming![0].GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/evrak/{incId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/evrak/{incId}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_direction_is_rejected()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var res = await client.PostAsJsonAsync("/api/evrak", new { direction = "sideways", title = "X" }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task One_office_never_sees_another_offices_evrak()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientA = factory.ClientFor(officeA, userA);
        var clientB = factory.ClientFor(officeB, userB);

        var created = await (await clientA.PostAsJsonAsync("/api/evrak", new { direction = "internal", title = "Gizli" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();

        var listB = await clientB.GetFromJsonAsync<List<JsonElement>>("/api/evrak", Json);
        Assert.DoesNotContain(listB!, e => e.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.DeleteAsync($"/api/evrak/{id}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/evrak")).StatusCode);
    }
}
