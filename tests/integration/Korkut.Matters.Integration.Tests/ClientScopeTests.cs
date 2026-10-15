using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.TestSupport;

namespace Korkut.Matters.Integration.Tests;

/// <summary>
/// Clients over the real API and PostgreSQL: a member sees the clients of their matters and the
/// ones they added (the full list takes clients.view_all), contact and identity details are left
/// out without their permissions (also on the matter page), and an update by someone who cannot see
/// a detail leaves it as it was.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class ClientScopeTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private async Task<Guid> AddMemberAsync(Guid office, string role)
    {
        await using var db = factory.OwnerContext();
        var user = new User(office, $"uye-{Guid.CreateVersion7():N}@example.com", role, "Üye", null, DateTimeOffset.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> CreateAsync(HttpClient client, string url, object body)
    {
        var created = await client.PostAsJsonAsync(url, body, Json);
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        return (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    private static async Task<HashSet<Guid>> ClientIdsAsync(HttpClient client)
    {
        var body = await client.GetFromJsonAsync<JsonElement>("/api/clients", Json);
        return (body.ValueKind == JsonValueKind.Array ? body : body.GetProperty("items")).EnumerateArray()
            .Select(c => c.GetProperty("id").GetGuid()).ToHashSet();
    }

    [Fact]
    public async Task Members_see_their_matters_clients_with_only_the_details_they_may_see()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var lawyerId = await AddMemberAsync(office, Roles.Lawyer);
        var internId = await AddMemberAsync(office, Roles.Intern);
        var owner = factory.ClientFor(office, ownerId);
        var lawyer = factory.ClientFor(office, lawyerId, Roles.Lawyer);
        var intern = factory.ClientFor(office, internId, Roles.Intern);
        var secretary = factory.ClientFor(office, await AddMemberAsync(office, Roles.Secretary), Roles.Secretary);

        var theirs = await CreateAsync(owner, "/api/clients", new { full_name = "Ayşe Yılmaz", national_id = "10000000146", phone = "05551112233" });
        var other = await CreateAsync(owner, "/api/clients", new { full_name = "Başka Müvekkil" });
        var matter = await CreateAsync(owner, "/api/matters", new { title = "Alacak davası", client_id = theirs });
        foreach (var member in new[] { lawyerId, internId })
        {
            Assert.True((await owner.PostAsJsonAsync($"/api/matters/{matter}/team", new { user_id = member }, Json)).IsSuccessStatusCode);
        }

        // The lawyer sees their matter's client in full, and not the other one.
        Assert.Equal([theirs], await ClientIdsAsync(lawyer));
        Assert.Equal(HttpStatusCode.NotFound, (await lawyer.GetAsync($"/api/clients/{other}")).StatusCode);
        var full = await lawyer.GetFromJsonAsync<JsonElement>($"/api/clients/{theirs}", Json);
        Assert.Equal("10000000146", full.GetProperty("national_id").GetString());
        Assert.False(full.GetProperty("identity_hidden").GetBoolean());

        // The intern sees the name only, here and on the matter page.
        var masked = await intern.GetFromJsonAsync<JsonElement>($"/api/clients/{theirs}", Json);
        Assert.Equal("Ayşe Yılmaz", masked.GetProperty("full_name").GetString());
        Assert.Equal(JsonValueKind.Null, masked.GetProperty("national_id").ValueKind);
        Assert.Equal(JsonValueKind.Null, masked.GetProperty("phone").ValueKind);
        Assert.True(masked.GetProperty("identity_hidden").GetBoolean());
        Assert.True(masked.GetProperty("contact_hidden").GetBoolean());
        var card = (await intern.GetFromJsonAsync<JsonElement>($"/api/matters/{matter}", Json)).GetProperty("client");
        Assert.Equal(JsonValueKind.Null, card.GetProperty("national_id").ValueKind);

        // The secretary keeps the office's client list.
        Assert.Superset(new HashSet<Guid> { theirs, other }, await ClientIdsAsync(secretary));

        // A client the lawyer adds is theirs before it has a matter.
        var added = await CreateAsync(lawyer, "/api/clients", new { full_name = "Yeni Müvekkil" });
        Assert.Contains(added, await ClientIdsAsync(lawyer));
    }

    [Fact]
    public async Task An_update_never_overwrites_details_the_caller_cannot_see()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        await using (var db = factory.OwnerContext())
        {
            db.OfficeRoles.Add(new OfficeRole(office, "custom-editor", "Kayıt görevlisi", null,
                [Permissions.ClientsRead, Permissions.ClientsWrite, Permissions.ClientsViewAll], false, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var owner = factory.ClientFor(office, ownerId);
        var editor = factory.ClientFor(office, await AddMemberAsync(office, "custom-editor"), "custom-editor");
        var client = await CreateAsync(owner, "/api/clients", new { full_name = "Ayşe Yılmaz", national_id = "10000000146", phone = "05551112233" });

        var renamed = await editor.PatchAsJsonAsync($"/api/clients/{client}", new { full_name = "Ayşe Yılmaz Kaya", national_id = (string?)null, phone = (string?)null }, Json);
        Assert.True(renamed.IsSuccessStatusCode, await renamed.Content.ReadAsStringAsync());

        var stored = await owner.GetFromJsonAsync<JsonElement>($"/api/clients/{client}", Json);
        Assert.Equal("Ayşe Yılmaz Kaya", stored.GetProperty("full_name").GetString());
        Assert.Equal("10000000146", stored.GetProperty("national_id").GetString());
        Assert.Equal("05551112233", stored.GetProperty("phone").GetString());
    }
}
