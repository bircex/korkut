using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.TestSupport;

namespace Korkut.Organization.Integration.Tests;

/// <summary>
/// The office's roles over the real API and PostgreSQL with RLS: every office has the system roles,
/// only the right people see and change them, a change applies to the very next request, offices
/// add their own roles and give them to members, and the safety rules hold (the owner's grants stay
/// complete, a role in use or a system role cannot be removed, unknown roles and permissions are refused).
/// </summary>
[Collection(IntegrationSuite.Name)]
public class RoleEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private async Task<Guid> AddMemberAsync(Guid office, string role)
    {
        await using var db = factory.OwnerContext();
        var user = new User(office, $"uye-{Guid.CreateVersion7():N}@example.com", role, null, null, DateTimeOffset.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static Task<HttpResponseMessage> OpenMatterAsync(HttpClient client)
        => client.PostAsJsonAsync("/api/matters", new { title = $"Dosya {Guid.NewGuid():N}" }, Json);

    private static async Task<JsonElement> RoleAsync(HttpClient owner, string key)
    {
        var roles = await owner.GetFromJsonAsync<JsonElement>("/api/roles", Json);
        return roles.GetProperty("roles").EnumerateArray().Single(r => r.GetProperty("key").GetString() == key);
    }

    [Fact]
    public async Task Every_office_has_the_system_roles_and_the_catalog()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        await AddMemberAsync(office, Roles.Lawyer);
        var owner = factory.ClientFor(office, ownerId);

        var body = await owner.GetFromJsonAsync<JsonElement>("/api/roles", Json);

        var roles = body.GetProperty("roles").EnumerateArray().ToList();
        Assert.Equal(["owner", "lawyer", "intern", "secretary", "accounting", "reader"], roles.Select(r => r.GetProperty("key").GetString()));
        Assert.All(roles, r => Assert.True(r.GetProperty("is_system").GetBoolean()));
        Assert.False(roles[0].GetProperty("editable").GetBoolean()); // the owner's grants are fixed
        Assert.Equal(1, roles[1].GetProperty("member_count").GetInt32());
        Assert.Contains(body.GetProperty("catalog").EnumerateArray(), g => g.GetProperty("name").GetString() == "Müvekkiller");
    }

    [Fact]
    public async Task Only_members_managers_see_roles_and_only_role_managers_change_them()
    {
        var (office, _) = await factory.SeedOfficeAsync();
        var lawyer = factory.ClientFor(office, await AddMemberAsync(office, Roles.Lawyer), Roles.Lawyer);

        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.GetAsync("/api/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await lawyer.PutAsJsonAsync("/api/roles/lawyer", new { name = "Avukat", permissions = Permissions.All }, Json)).StatusCode);
    }

    [Fact]
    public async Task A_changed_role_applies_on_the_next_request()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var owner = factory.ClientFor(office, ownerId);
        var lawyer = factory.ClientFor(office, await AddMemberAsync(office, Roles.Lawyer), Roles.Lawyer);

        Assert.Equal(HttpStatusCode.Created, (await OpenMatterAsync(lawyer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.GetAsync("/api/finance/invoices")).StatusCode);

        var withoutCreate = Roles.BuiltIn[Roles.Lawyer].Where(p => p != Permissions.MattersCreate).ToList();
        var saved = await owner.PutAsJsonAsync("/api/roles/lawyer", new { name = "Avukat", permissions = withoutCreate }, Json);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await OpenMatterAsync(lawyer)).StatusCode);
    }

    [Fact]
    public async Task An_office_adds_its_own_role_and_gives_it_to_a_member()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var owner = factory.ClientFor(office, ownerId);
        var memberId = await AddMemberAsync(office, Roles.Lawyer);

        var refused = await owner.PostAsJsonAsync("/api/roles",
            new { name = "Dış danışman", permissions = new[] { Permissions.MattersRead, "made.up" } }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);

        var created = await owner.PostAsJsonAsync("/api/roles",
            new { name = "Dış danışman", description = "Yalnızca okur ve sohbet eder", permissions = new[] { Permissions.MattersRead, Permissions.ChatUse } }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var key = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("key").GetString()!;
        Assert.StartsWith("custom-", key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await owner.PatchAsJsonAsync($"/api/members/{memberId}", new { role = "no-such-role" }, Json)).StatusCode);
        Assert.True((await owner.PatchAsJsonAsync($"/api/members/{memberId}", new { role = key }, Json)).IsSuccessStatusCode);

        // Signed in again, the member holds exactly what the role grants.
        var member = factory.ClientFor(office, memberId, key);
        var me = await member.GetFromJsonAsync<JsonElement>("/api/me", Json);
        Assert.Equal("Dış danışman", me.GetProperty("role_name").GetString());
        Assert.Equal([Permissions.ChatUse, Permissions.MattersRead], me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await OpenMatterAsync(member)).StatusCode);

        // A role someone holds, or a system role, cannot be removed; an unused one can.
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"/api/roles/{key}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync("/api/roles/intern")).StatusCode);
        Assert.True((await owner.PatchAsJsonAsync($"/api/members/{memberId}", new { role = Roles.Lawyer }, Json)).IsSuccessStatusCode);
        Assert.True((await owner.DeleteAsync($"/api/roles/{key}")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task The_owners_grants_cannot_be_narrowed()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var owner = factory.ClientFor(office, ownerId);

        var saved = await owner.PutAsJsonAsync("/api/roles/owner", new { name = "Yönetici", permissions = Array.Empty<string>() }, Json);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(Permissions.All.Count, (await RoleAsync(owner, Roles.OfficeOwner)).GetProperty("permissions").GetArrayLength());
        Assert.Equal(HttpStatusCode.Created, (await OpenMatterAsync(owner)).StatusCode);
    }

    [Fact]
    public async Task An_invite_names_a_role_the_office_has()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var owner = factory.ClientFor(office, ownerId);

        var unknown = await owner.PostAsJsonAsync("/api/invites", new { email = "yeni@example.com", role = "no-such-role" }, Json);
        var known = await owner.PostAsJsonAsync("/api/invites", new { email = "stajyer@example.com", role = Roles.Intern }, Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        Assert.True(known.IsSuccessStatusCode);
    }
}
