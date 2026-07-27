using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;
using Korkut.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Api.Tests;

/// <summary>
/// Platform-admin office management over the real API + real PostgreSQL (endpoint
/// migration, slice 9): a separate admin audience, cross-tenant office list +
/// enable/disable + stats. Office tokens must be rejected.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class AdminEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Admin_can_list_and_toggle_offices()
    {
        var (office, _) = await factory.SeedOfficeAsync();
        var admin = factory.AdminClient();

        // List includes the seeded office (cross-tenant).
        var list = await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/offices", Json);
        Assert.Contains(list!, o => o.GetProperty("id").GetGuid() == office);

        // Disable then enable.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/admin/offices/{office}/disable", null)).StatusCode);
        await using (var db = factory.OwnerContext())
        {
            Assert.True(await db.Offices.AsNoTracking().Where(x => x.Id == office).Select(x => x.Disabled).SingleAsync());
        }

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/admin/offices/{office}/enable", null)).StatusCode);
        await using (var db = factory.OwnerContext())
        {
            Assert.False(await db.Offices.AsNoTracking().Where(x => x.Id == office).Select(x => x.Disabled).SingleAsync());
        }
    }

    [Fact]
    public async Task Admin_stats_report_office_counts()
    {
        await factory.SeedOfficeAsync();
        var admin = factory.AdminClient();
        var stats = await admin.GetFromJsonAsync<JsonElement>("/api/admin/stats", Json);
        Assert.True(stats.GetProperty("offices").GetInt32() >= 1);
        Assert.Equal(stats.GetProperty("offices").GetInt32(),
            stats.GetProperty("active_offices").GetInt32() + stats.GetProperty("disabled_offices").GetInt32());
    }

    [Fact]
    public async Task An_office_token_is_rejected_at_the_admin_audience()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var officeClient = factory.ClientFor(office, user); // office-audience token
        Assert.Equal(HttpStatusCode.Unauthorized, (await officeClient.GetAsync("/api/admin/offices")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected_at_the_admin_audience()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/admin/offices")).StatusCode);
    }
}
