using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;
using Korkut.Application.Security;

namespace Korkut.Api.Tests;

/// <summary>
/// Document template catalog + overrides and the petition-type picker over the real API
/// + real PostgreSQL with RLS (endpoint migration, slice 16a). The catalog is static
/// reference data (korkut-backend/templates/); overrides are per office/user in the DB.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class TemplateEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task Petition_types_come_from_the_catalog()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var cats = await client.GetFromJsonAsync<List<JsonElement>>("/api/petition-types", Json);
        Assert.NotEmpty(cats!);
        var allTypes = cats!.SelectMany(c => c.GetProperty("dilekceler").EnumerateArray()).ToList();
        var bosanma = allTypes.Single(t => t.GetProperty("id").GetString() == "anlasmali-bosanma");
        Assert.Equal("Aile Mahkemesi", bosanma.GetProperty("mahkeme").GetString());
        Assert.True(bosanma.GetProperty("sik").GetBoolean());
    }

    [Fact]
    public async Task Template_list_and_detail_expose_default_and_overrides()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/templates?kind=dilekce", Json);
        Assert.Equal("dilekce", list.GetProperty("kind").GetString());
        var rows = list.GetProperty("kategoriler").EnumerateArray().SelectMany(k => k.GetProperty("tipler").EnumerateArray()).ToList();
        var row = rows.Single(t => t.GetProperty("id").GetString() == "tanik-listesi");
        Assert.False(row.GetProperty("has_office_override").GetBoolean());
        Assert.False(row.GetProperty("has_user_override").GetBoolean());

        // Detail has the built-in default; effective == default with no override.
        var detail = await client.GetFromJsonAsync<JsonElement>("/api/templates/tanik-listesi", Json);
        Assert.Equal("dilekce", detail.GetProperty("kind").GetString());
        var def = detail.GetProperty("default").GetString();
        Assert.False(string.IsNullOrWhiteSpace(def));
        Assert.Equal(def, detail.GetProperty("effective").GetString());
        Assert.Null(detail.GetProperty("user_override").ValueKind == JsonValueKind.Null ? null : detail.GetProperty("user_override").GetString());
    }

    [Fact]
    public async Task User_override_wins_over_office_override_over_default()
    {
        var (office, owner) = await factory.SeedOfficeAsync();
        var ownerClient = factory.ClientFor(office, owner);

        // Office override (owner has office.settings).
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.PutAsJsonAsync("/api/templates/tanik-listesi", new { scope = "office", content = "OFFICE-CONTENT" }, Json)).StatusCode);
        var afterOffice = await ownerClient.GetFromJsonAsync<JsonElement>("/api/templates/tanik-listesi", Json);
        Assert.Equal("OFFICE-CONTENT", afterOffice.GetProperty("office_override").GetString());
        Assert.Equal("OFFICE-CONTENT", afterOffice.GetProperty("effective").GetString());

        // User override wins.
        await ownerClient.PutAsJsonAsync("/api/templates/tanik-listesi", new { scope = "user", content = "MY-CONTENT" }, Json);
        var afterUser = await ownerClient.GetFromJsonAsync<JsonElement>("/api/templates/tanik-listesi", Json);
        Assert.Equal("MY-CONTENT", afterUser.GetProperty("effective").GetString());

        // Listing reflects both flags.
        var list = await ownerClient.GetFromJsonAsync<JsonElement>("/api/templates?kind=dilekce", Json);
        var row = list.GetProperty("kategoriler").EnumerateArray().SelectMany(k => k.GetProperty("tipler").EnumerateArray())
            .Single(t => t.GetProperty("id").GetString() == "tanik-listesi");
        Assert.True(row.GetProperty("has_office_override").GetBoolean());
        Assert.True(row.GetProperty("has_user_override").GetBoolean());

        // Delete the user override → effective falls back to office.
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.DeleteAsync("/api/templates/tanik-listesi?scope=user")).StatusCode);
        var afterDelete = await ownerClient.GetFromJsonAsync<JsonElement>("/api/templates/tanik-listesi", Json);
        Assert.Equal("OFFICE-CONTENT", afterDelete.GetProperty("effective").GetString());
    }

    [Fact]
    public async Task Members_cannot_write_office_templates()
    {
        var (office, owner) = await factory.SeedOfficeAsync();
        var lawyer = factory.ClientFor(office, owner, Roles.Lawyer);

        // Office scope forbidden for a non-owner.
        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.PutAsJsonAsync("/api/templates/tanik-listesi", new { scope = "office", content = "x" }, Json)).StatusCode);
        // But a lawyer may set their OWN override.
        Assert.Equal(HttpStatusCode.OK, (await lawyer.PutAsJsonAsync("/api/templates/tanik-listesi", new { scope = "user", content = "mine" }, Json)).StatusCode);
    }

    [Fact]
    public async Task Unknown_type_is_404()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/templates/does-not-exist")).StatusCode);
    }

    [Fact]
    public async Task One_office_never_sees_another_offices_template_overrides()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientA = factory.ClientFor(officeA, userA);
        var clientB = factory.ClientFor(officeB, userB);

        await clientA.PutAsJsonAsync("/api/templates/tanik-listesi", new { scope = "office", content = "A-ONLY" }, Json);
        var bDetail = await clientB.GetFromJsonAsync<JsonElement>("/api/templates/tanik-listesi", Json);
        Assert.Equal(JsonValueKind.Null, bDetail.GetProperty("office_override").ValueKind);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/petition-types")).StatusCode);
    }
}
