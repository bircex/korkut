using Korkut.TestSupport;
using Korkut.Core;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Security;

namespace Korkut.Matters.Tests;

/// <summary>
/// Office-core endpoints over the REAL API + real PostgreSQL with RLS in force
/// (KDM endpoint migration). Proves the full path: HTTP → auth → tenant scope → store
/// → DB, plus tenant isolation and deny-by-default authorization.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class OfficeCoreEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record ClientDto(Guid Id, string Name, string Kind, string? Email, string? Phone);

    [Fact]
    public async Task Client_crud_round_trips_over_http()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        // Create.
        var create = await client.PostAsJsonAsync("/api/clients", new { name = "Ahmet Yılmaz", kind = "individual", email = "ahmet@example.com" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ClientDto>(Json);
        Assert.Equal("Ahmet Yılmaz", created!.Name);

        // List shows it.
        var list = await client.GetFromJsonAsync<List<ClientDto>>("/api/clients", Json);
        Assert.Contains(list!, c => c.Id == created.Id);

        // Get by id.
        var got = await client.GetFromJsonAsync<ClientDto>($"/api/clients/{created.Id}", Json);
        Assert.Equal("ahmet@example.com", got!.Email);

        // Update.
        var patch = await client.PatchAsJsonAsync($"/api/clients/{created.Id}", new { name = "Ahmet K. Yılmaz", kind = "individual" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = await patch.Content.ReadFromJsonAsync<ClientDto>(Json);
        Assert.Equal("Ahmet K. Yılmaz", updated!.Name);

        // Delete (soft) → gone from list + 404 on get.
        var del = await client.DeleteAsync($"/api/clients/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        var afterList = await client.GetFromJsonAsync<List<ClientDto>>("/api/clients", Json);
        Assert.DoesNotContain(afterList!, c => c.Id == created.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/clients/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Clients_are_tenant_isolated_by_rls()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();

        // Office A creates a client.
        var a = factory.ClientFor(officeA, userA);
        var created = await (await a.PostAsJsonAsync("/api/clients", new { name = "A-only", kind = "individual" }))
            .Content.ReadFromJsonAsync<ClientDto>(Json);

        // Office B must NOT see it (RLS) — empty list + 404 on direct get.
        var b = factory.ClientFor(officeB, userB);
        var bList = await b.GetFromJsonAsync<List<ClientDto>>("/api/clients", Json);
        Assert.DoesNotContain(bList!, c => c.Id == created!.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/clients/{created!.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_lawyer_cannot_delete_a_matter_but_an_owner_can()
    {
        var (office, owner) = await factory.SeedOfficeAsync(Roles.OfficeOwner);

        // Owner creates a matter.
        var ownerClient = factory.ClientFor(office, owner, Roles.OfficeOwner);
        var create = await ownerClient.PostAsJsonAsync("/api/matters", new { title = "Dava 1" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var matter = await create.Content.ReadFromJsonAsync<JsonElement>();
        var matterId = matter.GetProperty("id").GetGuid();

        // A lawyer (same office) may read but NOT delete (deny-by-default; matters.delete is owner-only).
        var lawyer = factory.ClientFor(office, Guid.CreateVersion7(), Roles.Lawyer);
        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.DeleteAsync($"/api/matters/{matterId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ownerClient.DeleteAsync($"/api/matters/{matterId}")).StatusCode);
    }

    [Fact]
    public async Task Adding_a_hearing_surfaces_it_in_the_matter_detail()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var matterId = (await (await client.PostAsJsonAsync("/api/matters", new { title = "Duruşmalı Dosya" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Add a hearing (date required, time optional).
        var add = await client.PostAsJsonAsync($"/api/matters/{matterId}/hearings",
            new { date = "2026-09-15", time = "10:30:00", type = "Ön inceleme", location = "Ankara Adliyesi" });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var hearing = await add.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ön inceleme", hearing.GetProperty("type").GetString());
        Assert.Equal("pending", hearing.GetProperty("reminder_status").GetString());

        // The matter detail now lists it.
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/matters/{matterId}");
        var hearings = detail.GetProperty("hearings");
        Assert.Equal(1, hearings.GetArrayLength());
        Assert.Equal("Ankara Adliyesi", hearings[0].GetProperty("location").GetString());
        Assert.Contains("2026-09-15T10:30", hearings[0].GetProperty("scheduled_at").GetString());
    }

    [Fact]
    public async Task Adding_a_hearing_to_a_missing_matter_is_404()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var resp = await client.PostAsJsonAsync($"/api/matters/{Guid.CreateVersion7()}/hearings", new { date = "2026-09-15" });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task A_hearing_without_a_date_is_422()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var matterId = (await (await client.PostAsJsonAsync("/api/matters", new { title = "D" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var resp = await client.PostAsJsonAsync($"/api/matters/{matterId}/hearings", new { type = "Duruşma" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_requests_are_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/clients")).StatusCode);
    }

    [Fact]
    public async Task Invalid_client_payload_is_422()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var resp = await client.PostAsJsonAsync("/api/clients", new { name = "", kind = "individual" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }
}
