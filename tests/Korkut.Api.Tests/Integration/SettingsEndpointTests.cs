using Korkut.TestSupport;
using Korkut.Core;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;
using Korkut.Security;

namespace Korkut.Api.Tests;

/// <summary>
/// Office settings — persona, drafting style (office+user overlay), and memory — over
/// the real API + real PostgreSQL with RLS (endpoint migration, slice 13). Covers the
/// upserts, the effective-style overlay, the owner/member permission split, memory
/// approval, and tenant isolation.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class SettingsEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task Persona_upsert_and_can_edit_flag()
    {
        var (office, owner) = await factory.SeedOfficeAsync();
        var ownerClient = factory.ClientFor(office, owner); // owner → office.settings

        // Empty by default; owner can_edit true.
        var initial = await ownerClient.GetFromJsonAsync<JsonElement>("/api/persona", Json);
        Assert.Equal("", initial.GetProperty("tone").GetString());
        Assert.True(initial.GetProperty("can_edit").GetBoolean());

        var put = await ownerClient.PutAsJsonAsync("/api/persona", new { tone = "resmi", expertise = "iş hukuku", extraRules = "kısa yaz" }, Json);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var saved = await ownerClient.GetFromJsonAsync<JsonElement>("/api/persona", Json);
        Assert.Equal("iş hukuku", saved.GetProperty("expertise").GetString());

        // A lawyer can read but not edit (403 on PUT; can_edit false).
        var lawyer = factory.ClientFor(office, owner, Roles.Lawyer);
        var lawyerView = await lawyer.GetFromJsonAsync<JsonElement>("/api/persona", Json);
        Assert.False(lawyerView.GetProperty("can_edit").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.PutAsJsonAsync("/api/persona", new { tone = "x" }, Json)).StatusCode);
    }

    [Fact]
    public async Task Effective_style_overlays_user_over_office()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        await client.PutAsJsonAsync("/api/style/office",
            new { letterhead = "OFFICE LH", signature = "Ofis", tone = "resmi", boilerplate = "std" }, Json);
        // User overrides only signature + tone; letterhead/boilerplate fall through to office.
        await client.PutAsJsonAsync("/api/style/user",
            new { letterhead = "", signature = "Av. X", tone = "samimi", boilerplate = "" }, Json);

        var style = await client.GetFromJsonAsync<JsonElement>("/api/style", Json);
        var eff = style.GetProperty("effective");
        Assert.Equal("OFFICE LH", eff.GetProperty("letterhead").GetString()); // from office
        Assert.Equal("Av. X", eff.GetProperty("signature").GetString());      // from user
        Assert.Equal("samimi", eff.GetProperty("tone").GetString());          // from user
        Assert.Equal("std", eff.GetProperty("boilerplate").GetString());      // from office
        Assert.True(style.GetProperty("can_edit_office").GetBoolean());
    }

    [Fact]
    public async Task User_style_is_per_user_not_shared()
    {
        var (office, userA) = await factory.SeedOfficeAsync();
        var clientA = factory.ClientFor(office, userA);
        var clientB = factory.ClientFor(office, Guid.CreateVersion7()); // same office, different user id

        await clientA.PutAsJsonAsync("/api/style/user", new { signature = "A-imza" }, Json);
        var bStyle = await clientB.GetFromJsonAsync<JsonElement>("/api/style", Json);
        Assert.Equal("", bStyle.GetProperty("user").GetProperty("signature").GetString()); // B has no personal style
    }

    [Fact]
    public async Task Memory_owner_active_member_pending_then_approve()
    {
        var (office, owner) = await factory.SeedOfficeAsync();
        var ownerClient = factory.ClientFor(office, owner);
        var lawyer = factory.ClientFor(office, Guid.CreateVersion7(), Roles.Lawyer);

        // Owner-added → active.
        var byOwner = await (await ownerClient.PostAsJsonAsync("/api/memory", new { text = "Mahkeme X katıdır", kind = "court" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("active", byOwner.GetProperty("status").GetString());

        // Member-added → pending; owner approves → active.
        var byMember = await (await lawyer.PostAsJsonAsync("/api/memory", new { text = "Müvekkil telefonla ulaşılır" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var memId = byMember.GetProperty("id").GetGuid();
        Assert.Equal("pending", byMember.GetProperty("status").GetString());

        // A member cannot approve (office.settings required).
        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.PostAsync($"/api/memory/{memId}/approve", null)).StatusCode);

        var approved = await ownerClient.PostAsync($"/api/memory/{memId}/approve", null);
        Assert.Equal("active", (await approved.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString());

        var list = await ownerClient.GetFromJsonAsync<JsonElement>("/api/memory", Json);
        Assert.Equal(2, list.GetProperty("items").GetArrayLength());

        Assert.Equal(HttpStatusCode.NoContent, (await ownerClient.DeleteAsync($"/api/memory/{memId}")).StatusCode);
    }

    [Fact]
    public async Task One_office_never_sees_another_offices_settings()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientA = factory.ClientFor(officeA, userA);
        var clientB = factory.ClientFor(officeB, userB);

        await clientA.PutAsJsonAsync("/api/persona", new { tone = "A-tonu" }, Json);
        var bPersona = await clientB.GetFromJsonAsync<JsonElement>("/api/persona", Json);
        Assert.Equal("", bPersona.GetProperty("tone").GetString()); // B's persona untouched

        var created = await (await clientA.PostAsJsonAsync("/api/memory", new { text = "A-gizli" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.DeleteAsync($"/api/memory/{id}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/style")).StatusCode);
    }
}
