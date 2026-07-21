using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Korkut.Agent.Tests.Integration;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Agent.Tests;

/// <summary>
/// Session file upload over the real Agent host + real PostgreSQL. A document is attached to
/// a conversation; its text is extracted and included in the user turn so the (fake) assistant
/// can reference it.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class SessionUploadEndpointTests(KorkutAgentFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static MultipartFormDataContent File(string name, string content, string text)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", name);
        form.Add(new StringContent(text), "text");
        return form;
    }

    [Fact]
    public async Task Upload_extracts_text_and_the_assistant_sees_it()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var created = await (await client.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();

        var res = await client.PostAsync($"/api/sessions/{id}/upload",
            File("sozlesme.txt", "Bu bir kira sözleşmesidir. Aylık kira 10000 TL.", "Bu belgeyi özetle"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);

        // The fake echoes the last user turn, which now carries the extracted document text.
        Assert.Contains("kira sözleşmesidir", body.GetProperty("reply").GetString());
        Assert.Contains("sozlesme.txt", body.GetProperty("user_display").GetString());

        // Persisted: a user turn (with the extracted content) + an assistant reply (Agent DB).
        await using var db = factory.AgentOwnerContext();
        var count = await db.ChatMessages.AsNoTracking().CountAsync(m => m.ConversationId == id);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Upload_without_a_file_is_rejected()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var created = await (await client.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();

        var form = new MultipartFormDataContent { { new StringContent("hi"), "text" } };
        var res = await client.PostAsync($"/api/sessions/{id}/upload", form);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Upload_to_another_offices_session_is_not_found()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientA = factory.ClientFor(officeA, userA);
        var clientB = factory.ClientFor(officeB, userB);
        var created = await (await clientA.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();

        var res = await clientB.PostAsync($"/api/sessions/{id}/upload", File("x.txt", "data", "t"));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        var res = await anon.PostAsync($"/api/sessions/{Guid.NewGuid()}/upload", File("x.txt", "d", "t"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
