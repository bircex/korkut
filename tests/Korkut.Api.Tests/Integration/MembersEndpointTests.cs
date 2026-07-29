using Korkut.TestSupport;
using Korkut.Core;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;
using Korkut.Security;

namespace Korkut.Api.Tests;

/// <summary>
/// Office-membership endpoints over the real API + real PostgreSQL with RLS (endpoint
/// migration, slice 5): owner-only management, role change, and the office-keeps-an-
/// owner / no-self-removal invariants.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class MembersEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Owner_can_add_list_and_change_a_member_role()
    {
        var (office, owner) = await factory.SeedOfficeAsync(Roles.OfficeOwner);
        var client = factory.ClientFor(office, owner, Roles.OfficeOwner);

        var create = await client.PostAsJsonAsync("/api/members", new { email = "yeni@example.com", role = "lawyer" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var memberId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/members", Json);
        Assert.Contains(list!, m => m.GetProperty("id").GetGuid() == memberId);

        var patch = await client.PatchAsJsonAsync($"/api/members/{memberId}", new { role = "owner" });
        Assert.Equal(HttpStatusCode.NoContent, patch.StatusCode);
    }

    [Fact]
    public async Task A_lawyer_cannot_manage_members()
    {
        var (office, _) = await factory.SeedOfficeAsync();
        var lawyer = factory.ClientFor(office, Guid.CreateVersion7(), Roles.Lawyer);
        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.GetAsync("/api/members")).StatusCode);
    }

    [Fact]
    public async Task The_last_owner_cannot_be_demoted()
    {
        var (office, owner) = await factory.SeedOfficeAsync(Roles.OfficeOwner);
        var client = factory.ClientFor(office, owner, Roles.OfficeOwner);
        // The seeded owner is the ONLY owner → demoting them is a 409.
        var patch = await client.PatchAsJsonAsync($"/api/members/{owner}", new { role = "lawyer" });
        Assert.Equal(HttpStatusCode.Conflict, patch.StatusCode);
    }

    [Fact]
    public async Task An_owner_cannot_remove_themselves()
    {
        var (office, owner) = await factory.SeedOfficeAsync(Roles.OfficeOwner);
        var client = factory.ClientFor(office, owner, Roles.OfficeOwner);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/members/{owner}")).StatusCode);
    }

    [Fact]
    public async Task Members_are_tenant_isolated()
    {
        var (officeA, ownerA) = await factory.SeedOfficeAsync(Roles.OfficeOwner);
        var (officeB, ownerB) = await factory.SeedOfficeAsync(Roles.OfficeOwner);

        var a = factory.ClientFor(officeA, ownerA, Roles.OfficeOwner);
        var listA = await a.GetFromJsonAsync<List<JsonElement>>("/api/members", Json);
        // Office A sees only its own owner, never office B's.
        Assert.DoesNotContain(listA!, m => m.GetProperty("id").GetGuid() == ownerB);
        Assert.Contains(listA!, m => m.GetProperty("id").GetGuid() == ownerA);
    }
}
