using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;

namespace Korkut.Api.Tests;

/// <summary>
/// Assistant coverage over the real API (endpoint migration, slice 16c). Coverage is
/// derived from the static catalog, so it reports real type counts, a per-court petition
/// breakdown, and the source legislation.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class CoverageEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task Coverage_reports_catalog_totals_and_courts()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var c = await client.GetFromJsonAsync<JsonElement>("/api/coverage", Json);
        Assert.True(c.GetProperty("total_decisions").GetInt32() > 0);

        var byCourt = c.GetProperty("by_court").EnumerateArray().ToList();
        Assert.NotEmpty(byCourt);
        // Family court appears (the petition catalog groups several dilekçe under it).
        Assert.Contains(byCourt, x => x.GetProperty("label").GetString() == "Aile Mahkemesi" && x.GetProperty("total").GetInt32() > 0);
        // Sorted by total descending.
        var totals = byCourt.Select(x => x.GetProperty("total").GetInt32()).ToList();
        Assert.True(totals.SequenceEqual(totals.OrderByDescending(t => t)));

        Assert.NotEmpty(c.GetProperty("decision_sources").EnumerateArray());
        Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("legislation").GetString()));
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/coverage")).StatusCode);
    }
}
