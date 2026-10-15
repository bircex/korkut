using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.TestSupport;

namespace Korkut.Matters.Integration.Tests;

/// <summary>
/// Matter scope over the real API and PostgreSQL with RLS: a member sees only the matters whose
/// team they are on (the owner sees every one), everything tied to a matter follows it, records
/// tied to no matter belong to whoever made them, other matters read as not found, and the team
/// is changed by the matter's lead.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class MatterScopeTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private async Task<Guid> AddLawyerAsync(Guid office)
    {
        await using var db = factory.OwnerContext();
        var user = new User(office, $"avukat-{Guid.CreateVersion7():N}@example.com", Roles.Lawyer, "Av. Deneme", null, DateTimeOffset.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> OpenMatterAsync(HttpClient client, string title)
    {
        var created = await client.PostAsJsonAsync("/api/matters", new { title }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    private static async Task<List<JsonElement>> ListAsync(HttpClient client, string url)
    {
        var body = await client.GetFromJsonAsync<JsonElement>(url, Json);
        return [.. (body.ValueKind == JsonValueKind.Array ? body : body.GetProperty("items")).EnumerateArray()];
    }

    private static async Task<HashSet<Guid>> MatterIdsAsync(HttpClient client)
        => (await ListAsync(client, "/api/matters")).Select(m => m.GetProperty("id").GetGuid()).ToHashSet();

    [Fact]
    public async Task Members_see_their_own_matters_and_the_owner_sees_all()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var (aId, bId) = (await AddLawyerAsync(office), await AddLawyerAsync(office));
        var owner = factory.ClientFor(office, ownerId);
        var a = factory.ClientFor(office, aId, Roles.Lawyer);
        var b = factory.ClientFor(office, bId, Roles.Lawyer);

        var mine = await OpenMatterAsync(a, "A'nın dosyası");
        var others = await OpenMatterAsync(owner, "Yönetimin dosyası");

        Assert.Equal([mine], await MatterIdsAsync(a));
        Assert.Empty(await MatterIdsAsync(b));
        Assert.Superset(new HashSet<Guid> { mine, others }, await MatterIdsAsync(owner));

        // Another matter reads as not found, never as forbidden.
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/matters/{others}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await a.PatchAsJsonAsync($"/api/matters/{others}", new { title = "değişti" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/matters/{mine}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/matters/{mine}")).StatusCode);
    }

    [Fact]
    public async Task What_is_tied_to_a_matter_follows_it()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var aId = await AddLawyerAsync(office);
        var owner = factory.ClientFor(office, ownerId);
        var a = factory.ClientFor(office, aId, Roles.Lawyer);
        var mine = await OpenMatterAsync(a, "A'nın dosyası");
        var others = await OpenMatterAsync(owner, "Yönetimin dosyası");
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)).ToString("yyyy-MM-dd");

        // Nothing can be attached to a matter the member does not see.
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsJsonAsync($"/api/matters/{others}/hearings", new { date = day }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsJsonAsync("/api/tasks", new { title = "x", matter_id = others }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsJsonAsync("/api/notes", new { body = "x", matter_id = others }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await a.PostAsJsonAsync("/api/deadlines", new { title = "x", due_date = day, matter_id = others }, Json)).StatusCode);

        // The owner's records on their matter stay out of the member's lists and calendar.
        Assert.True((await owner.PostAsJsonAsync($"/api/matters/{others}/hearings", new { date = day }, Json)).IsSuccessStatusCode);
        Assert.True((await owner.PostAsJsonAsync("/api/notes", new { body = "gizli not", matter_id = others }, Json)).IsSuccessStatusCode);
        Assert.True((await a.PostAsJsonAsync($"/api/matters/{mine}/hearings", new { date = day }, Json)).IsSuccessStatusCode);

        Assert.DoesNotContain(await ListAsync(a, "/api/notes"), n => n.GetProperty("body").GetString() == "gizli not");
        var calendar = await ListAsync(a, "/api/calendar/events");
        Assert.Contains(calendar, e => e.GetProperty("matter_id").GetGuid() == mine);
        Assert.DoesNotContain(calendar, e => e.TryGetProperty("matter_id", out var m) && m.ValueKind == JsonValueKind.String && m.GetGuid() == others);
    }

    [Fact]
    public async Task Records_with_no_matter_belong_to_whoever_made_them()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var (aId, bId) = (await AddLawyerAsync(office), await AddLawyerAsync(office));
        var owner = factory.ClientFor(office, ownerId);
        var a = factory.ClientFor(office, aId, Roles.Lawyer);
        var b = factory.ClientFor(office, bId, Roles.Lawyer);

        Assert.True((await a.PostAsJsonAsync("/api/tasks", new { title = "A'nın işi" }, Json)).IsSuccessStatusCode);
        Assert.True((await a.PostAsJsonAsync("/api/tasks", new { title = "B'ye verilen iş", assignee_id = bId }, Json)).IsSuccessStatusCode);

        Assert.Equal(["A'nın işi", "B'ye verilen iş"], (await ListAsync(a, "/api/tasks")).Select(t => t.GetProperty("title").GetString()).Order());
        Assert.Equal(["B'ye verilen iş"], (await ListAsync(b, "/api/tasks")).Select(t => t.GetProperty("title").GetString()));
        Assert.Equal(2, (await ListAsync(owner, "/api/tasks")).Count);
    }

    [Fact]
    public async Task The_lead_brings_people_onto_the_matter_and_takes_them_off()
    {
        var (office, _) = await factory.SeedOfficeAsync();
        var (aId, bId, cId) = (await AddLawyerAsync(office), await AddLawyerAsync(office), await AddLawyerAsync(office));
        var a = factory.ClientFor(office, aId, Roles.Lawyer);
        var b = factory.ClientFor(office, bId, Roles.Lawyer);
        var mine = await OpenMatterAsync(a, "Ekip dosyası");

        var team = await a.GetFromJsonAsync<JsonElement>($"/api/matters/{mine}/team", Json);
        var lead = Assert.Single(team.GetProperty("members").EnumerateArray());
        Assert.Equal(aId, lead.GetProperty("user_id").GetGuid());
        Assert.True(lead.GetProperty("is_lead").GetBoolean());
        Assert.True(team.GetProperty("can_manage").GetBoolean());
        var candidates = await a.GetFromJsonAsync<JsonElement>($"/api/matters/{mine}/team/candidates", Json);
        Assert.Contains(candidates.EnumerateArray(), c => c.GetProperty("user_id").GetGuid() == bId);

        Assert.True((await a.PostAsJsonAsync($"/api/matters/{mine}/team", new { user_id = bId }, Json)).IsSuccessStatusCode);
        Assert.Contains(mine, await MatterIdsAsync(b));

        // A member who is not the lead cannot change the team.
        Assert.Equal(HttpStatusCode.Forbidden, (await b.PostAsJsonAsync($"/api/matters/{mine}/team", new { user_id = cId }, Json)).StatusCode);
        Assert.False((await b.GetFromJsonAsync<JsonElement>($"/api/matters/{mine}/team", Json)).GetProperty("can_manage").GetBoolean());

        Assert.True((await a.DeleteAsync($"/api/matters/{mine}/team/{bId}")).IsSuccessStatusCode);
        Assert.DoesNotContain(mine, await MatterIdsAsync(b));
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/matters/{mine}/team")).StatusCode);
    }
}
