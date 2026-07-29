using Korkut.TestSupport;
using Korkut.Core;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;
using Korkut.Security;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Api.Tests;

/// <summary>
/// Office-invite endpoints over the real API + real PostgreSQL with RLS (endpoint
/// migration, slice 8): owner-only management, anonymous accept via the cross-tenant
/// token resolver (creates the member), single-use.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class InviteEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Owner_creates_an_invite_and_accepting_it_creates_the_member()
    {
        var (office, owner) = await factory.SeedOfficeAsync(Roles.OfficeOwner);
        var client = factory.ClientFor(office, owner, Roles.OfficeOwner);

        // Create → the raw token is returned once.
        var create = await client.PostAsJsonAsync("/api/invites", new { email = "davetli@example.com", role = "lawyer" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var token = created.GetProperty("token").GetString()!;

        // It shows in the owner's list.
        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/invites", Json);
        Assert.Contains(list!, i => i.GetProperty("email").GetString() == "davetli@example.com");

        // Accept (anonymous) → creates the member; the invite disappears from the list.
        var anon = factory.CookieClient();
        var accept = await anon.PostAsJsonAsync("/api/invites/accept", new { token });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        await using (var db = factory.OwnerContext())
        {
            var member = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == office && x.Email == "davetli@example.com");
            Assert.NotNull(member);
            Assert.Equal("lawyer", member!.Role);
        }
    }

    [Fact]
    public async Task An_invite_is_single_use()
    {
        var (office, owner) = await factory.SeedOfficeAsync(Roles.OfficeOwner);
        var client = factory.ClientFor(office, owner, Roles.OfficeOwner);
        var token = (await (await client.PostAsJsonAsync("/api/invites", new { email = "tek@example.com", role = "lawyer" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

        var anon = factory.CookieClient();
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsJsonAsync("/api/invites/accept", new { token })).StatusCode);
        // Second accept of the same token is rejected.
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/api/invites/accept", new { token })).StatusCode);
    }

    [Fact]
    public async Task An_invalid_token_is_400()
    {
        var anon = factory.CookieClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/api/invites/accept", new { token = "nope" })).StatusCode);
    }

    [Fact]
    public async Task A_lawyer_cannot_manage_invites_and_revoke_works()
    {
        var (office, owner) = await factory.SeedOfficeAsync(Roles.OfficeOwner);
        var lawyer = factory.ClientFor(office, Guid.CreateVersion7(), Roles.Lawyer);
        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.GetAsync("/api/invites")).StatusCode);

        var client = factory.ClientFor(office, owner, Roles.OfficeOwner);
        var id = (await (await client.PostAsJsonAsync("/api/invites", new { email = "iptal@example.com", role = "lawyer" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/invites/{id}")).StatusCode);
    }

    [Fact]
    public async Task Invites_are_tenant_isolated()
    {
        var (officeA, ownerA) = await factory.SeedOfficeAsync(Roles.OfficeOwner);
        var (officeB, ownerB) = await factory.SeedOfficeAsync(Roles.OfficeOwner);
        var a = factory.ClientFor(officeA, ownerA, Roles.OfficeOwner);
        await a.PostAsJsonAsync("/api/invites", new { email = "a-only@example.com", role = "lawyer" });

        var b = factory.ClientFor(officeB, ownerB, Roles.OfficeOwner);
        var bList = await b.GetFromJsonAsync<List<JsonElement>>("/api/invites", Json);
        Assert.DoesNotContain(bList!, i => i.GetProperty("email").GetString() == "a-only@example.com");
    }
}
