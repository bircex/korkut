using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Agent.Tests.Integration;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Agent.Tests;

/// <summary>
/// Streaming assistant chat (SSE) over the real Agent host + real PostgreSQL. The fake model
/// streams the echo word-by-word; the test parses the event stream (step/token/done) and
/// confirms the reply is persisted in the Agent DB.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class SessionStreamEndpointTests(KorkutAgentFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Chat_stream_emits_steps_tokens_and_a_done_frame()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var created = await (await client.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = created.GetProperty("id").GetGuid();

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{id}/chat/stream")
        {
            Content = JsonContent.Create(new { text = "Tazminat davası" }, options: Json),
        };
        var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/event-stream", res.Content.Headers.ContentType!.MediaType);

        var body = await res.Content.ReadAsStringAsync();
        var frames = ParseSse(body);

        Assert.Contains(frames, f => f.Event == "step");
        Assert.Contains(frames, f => f.Event == "token");
        var done = frames.Single(f => f.Event == "done");
        var payload = JsonSerializer.Deserialize<JsonElement>(done.Data);
        Assert.Contains("Tazminat davası", payload.GetProperty("reply").GetString());
        Assert.Equal("Tazminat davası", payload.GetProperty("title").GetString());
        // done payload uses the snake_case contract.
        Assert.True(payload.TryGetProperty("open_document", out _));

        // The streamed reply was persisted (user + assistant) in the Agent DB.
        await using var db = factory.AgentOwnerContext();
        var count = await db.ChatMessages.AsNoTracking().CountAsync(m => m.ConversationId == id);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Chat_stream_on_a_missing_session_emits_an_error_frame()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{Guid.NewGuid()}/chat/stream")
        {
            Content = JsonContent.Create(new { text = "x" }, options: Json),
        };
        var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var frames = ParseSse(await res.Content.ReadAsStringAsync());
        Assert.Contains(frames, f => f.Event == "error");
    }

    private static List<(string Event, string Data)> ParseSse(string body)
    {
        var frames = new List<(string, string)>();
        foreach (var block in body.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string ev = "message", data = "";
            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("event:", StringComparison.Ordinal)) ev = line[6..].Trim();
                else if (line.StartsWith("data:", StringComparison.Ordinal)) data += line[5..].Trim();
            }

            if (data.Length > 0) frames.Add((ev, data));
        }

        return frames;
    }
}
