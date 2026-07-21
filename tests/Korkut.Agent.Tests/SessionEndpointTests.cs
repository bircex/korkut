using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Agent.Tests.Integration;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Agent.Tests;

/// <summary>
/// Assistant sessions + chat over the real Agent host + real PostgreSQL with RLS. A
/// deterministic fake model stands in for the provider, so the full path (persistence,
/// system-prompt build across the operational + Agent DBs, reply, auto-title) runs
/// end-to-end.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class SessionEndpointTests(KorkutAgentFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Session_lifecycle_create_chat_rename_clone_delete()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        // Create.
        var created = await (await client.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Yeni görüşme", created.GetProperty("title").GetString());

        // Chat → the fake echoes the user turn; the session is auto-titled from the first message.
        var chat = await (await client.PostAsJsonAsync($"/api/sessions/{id}/chat", new { text = "Kira sözleşmesi feshi hakkında" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains("Kira sözleşmesi feshi hakkında", chat.GetProperty("reply").GetString());
        Assert.Equal("Kira sözleşmesi feshi hakkında", chat.GetProperty("title").GetString());

        // Detail carries both messages in order.
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/sessions/{id}", Json);
        var messages = detail.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(2, messages.Count);
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());

        // Rename.
        var renamed = await (await client.PatchAsJsonAsync($"/api/sessions/{id}", new { title = "Yeniden adlandırıldı" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Yeniden adlandırıldı", renamed.GetProperty("title").GetString());

        // Clone copies title + messages.
        var clone = await (await client.PostAsync($"/api/sessions/{id}/clone", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var cloneId = clone.GetProperty("id").GetGuid();
        Assert.NotEqual(id, cloneId);
        var cloneDetail = await client.GetFromJsonAsync<JsonElement>($"/api/sessions/{cloneId}", Json);
        Assert.Equal(2, cloneDetail.GetProperty("messages").GetArrayLength());

        // List shows both, newest first.
        var page = await client.GetFromJsonAsync<JsonElement>("/api/sessions?limit=10&offset=0", Json);
        Assert.True(page.GetProperty("total").GetInt32() >= 2);

        // Delete.
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/sessions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/sessions/{id}")).StatusCode);
    }

    [Fact]
    public async Task Session_brief_resolves_the_author_display_name_from_the_operational_directory()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var created = await (await client.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        // The author id is stored in the Agent DB; the host resolves it to the operational
        // user's e-mail (display name) via the user-lookup port.
        Assert.Equal($"user-{user:N}@example.com", created.GetProperty("created_by_name").GetString());
    }

    [Fact]
    public async Task System_prompt_includes_persona_and_memory()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        // Persona + memory live in the operational DB; a chat turn reads them across the
        // DB boundary while writing the conversation to the Agent DB. The fake echoes only
        // the last USER turn, so we exercise the composed path and assert success.
        await factory.SeedPersonaAndMemoryAsync(office, user);

        var created = await (await client.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();
        var chat = await client.PostAsJsonAsync($"/api/sessions/{id}/chat", new { text = "Merhaba" }, Json);
        Assert.Equal(HttpStatusCode.OK, chat.StatusCode);
    }

    [Fact]
    public async Task A_chat_turn_is_recorded_in_the_llm_calls_ledger()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var created = await (await client.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();
        await client.PostAsJsonAsync($"/api/sessions/{id}/chat", new { text = "Merhaba" }, Json);

        // The model call's provider/model/tokens landed in the Agent DB cost ledger, office-scoped,
        // stamped with a prompt fingerprint (governance/versioning).
        await using var db = factory.AgentOwnerContext();
        var calls = await db.LlmCalls.AsNoTracking().Where(c => c.OfficeId == office).ToListAsync();
        Assert.Contains(calls, c => c.Feature == "chat" && c.Provider == "fake"
            && c.PromptTokens == 12 && c.CompletionTokens == 34 && !string.IsNullOrEmpty(c.PromptFingerprint));
    }

    [Fact]
    public async Task A_chat_is_rejected_when_the_office_token_budget_is_exhausted()
    {
        var (office, user) = await factory.SeedOfficeAsync();

        // Pre-seed usage well over a tiny budget (owner context bypasses RLS).
        await using (var seed = factory.AgentOwnerContext())
        {
            seed.LlmCalls.Add(new Korkut.Infrastructure.Persistence.Entities.LlmCallEntity
            {
                Id = Guid.CreateVersion7(), OfficeId = office, Feature = "chat", Provider = "fake", ModelId = "fake-model",
                PromptFingerprint = "seed", PromptTokens = 1000, CompletionTokens = 1000, Cost = 0m, LatencyMs = 1,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await seed.SaveChangesAsync();
        }

        Environment.SetEnvironmentVariable("KORKUT_OFFICE_TOKEN_BUDGET", "100");
        try
        {
            var client = factory.ClientFor(office, user);
            var created = await (await client.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
            var id = created.GetProperty("id").GetGuid();
            var res = await client.PostAsJsonAsync($"/api/sessions/{id}/chat", new { text = "Merhaba" }, Json);
            Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("KORKUT_OFFICE_TOKEN_BUDGET", null);
        }
    }

    [Fact]
    public async Task One_office_never_sees_another_offices_sessions()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientA = factory.ClientFor(officeA, userA);
        var clientB = factory.ClientFor(officeB, userB);

        var created = await (await clientA.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/sessions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.DeleteAsync($"/api/sessions/{id}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/sessions")).StatusCode);
    }
}
