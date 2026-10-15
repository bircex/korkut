using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.TestSupport;

namespace Korkut.Matters.Integration.Tests;

/// <summary>
/// Conversations over the real API and PostgreSQL: a matter's conversations are its team's, a
/// personal one only its owner's (even the owner of the office does not read it), opening one takes
/// the matter or the right to personal conversations, and nobody writes into a conversation they
/// cannot see.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class ConversationScopeTests(KorkutApiFactory factory)
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

    private static async Task<Guid> OpenChatAsync(HttpClient client, Guid? matterId = null)
    {
        var created = await client.PostAsync(matterId is { } m ? $"/api/sessions?matterId={m}" : "/api/sessions", null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    private static async Task<HashSet<Guid>> ChatIdsAsync(HttpClient client, string query = "")
        => (await client.GetFromJsonAsync<JsonElement>($"/api/sessions{query}", Json)).GetProperty("items").EnumerateArray()
            .Select(s => s.GetProperty("id").GetGuid()).ToHashSet();

    [Fact]
    public async Task A_matters_conversations_are_its_teams_and_a_personal_one_is_its_owners()
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        var (aId, bId) = (await AddMemberAsync(office, Roles.Lawyer), await AddMemberAsync(office, Roles.Lawyer));
        var owner = factory.ClientFor(office, ownerId);
        var a = factory.ClientFor(office, aId, Roles.Lawyer);
        var b = factory.ClientFor(office, bId, Roles.Lawyer);
        var matter = (await (await a.PostAsJsonAsync("/api/matters", new { title = "Dosya" }, Json)).Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var onMatter = await OpenChatAsync(a, matter);
        var personal = await OpenChatAsync(a);

        Assert.Equal(new HashSet<Guid> { onMatter, personal }, await ChatIdsAsync(a));
        Assert.Equal([onMatter], await ChatIdsAsync(a, $"?matterId={matter}"));
        Assert.Equal([personal], await ChatIdsAsync(a, "?personal=true"));

        // B is not on the matter: neither conversation exists for them, and they cannot write into one.
        Assert.Empty(await ChatIdsAsync(b));
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/sessions/{onMatter}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/sessions/{onMatter}/chat", new { text = "merhaba" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/sessions/{onMatter}/document")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/sessions?matterId={matter}", null)).StatusCode);

        // The office's owner sees the matter's conversation, never A's personal one.
        Assert.Contains(onMatter, await ChatIdsAsync(owner));
        Assert.DoesNotContain(personal, await ChatIdsAsync(owner));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/sessions/{personal}")).StatusCode);

        // Once on the team, B reads the matter's conversation, but only A, who opened it, removes it.
        Assert.True((await a.PostAsJsonAsync($"/api/matters/{matter}/team", new { user_id = bId }, Json)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync($"/api/sessions/{onMatter}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.DeleteAsync($"/api/sessions/{onMatter}")).StatusCode);
        Assert.True((await a.DeleteAsync($"/api/sessions/{onMatter}")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Personal_conversations_take_their_own_permission()
    {
        var (office, _) = await factory.SeedOfficeAsync();
        var reader = factory.ClientFor(office, await AddMemberAsync(office, Roles.Reader), Roles.Reader);

        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsync("/api/sessions", null)).StatusCode);
    }
}
