using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Application.Knowledge;
using Korkut.Application.Runtime;
using Korkut.Domain.Knowledge;

namespace Korkut.Agent.Knowledge;

/// <summary>
/// The Agent-side <see cref="IKnowledgeSearchService"/> adapter: calls the internal Korkut.Knowledge
/// <c>/search</c> endpoint over HTTP, forwarding the caller's bearer token so Knowledge derives
/// the office itself (RLS) — the office is never sent in the body. Configured by
/// <c>KORKUT_KNOWLEDGE_URL</c>; when that is unset, or the call fails, or there is no caller token,
/// it returns an EMPTY list so a chat turn degrades to ungrounded rather than failing. Payload and
/// response use the shared snake_case contract. Lives in the host (not Infrastructure) because it
/// reads the ambient <see cref="HttpContext"/> to forward the token.
/// </summary>
public sealed class HttpKnowledgeSearchService(HttpClient http, IHttpContextAccessor accessor, IEnvironment env) : IKnowledgeSearchService
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var baseUrl = env.Get("KORKUT_KNOWLEDGE_URL")?.Trim();
        var token = BearerToken(accessor.HttpContext);
        if (string.IsNullOrWhiteSpace(baseUrl) || token is null)
        {
            return []; // not configured / no caller identity → ungrounded, never an error
        }

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/search")
            {
                Content = JsonContent.Create(new SearchBody(query, topK), options: Json),
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var body = await response.Content.ReadFromJsonAsync<SearchResult>(Json, ct);
            return body?.Hits is null
                ? []
                : body.Hits.Select(h => new SearchHit(h.ChunkId, h.DocumentId, h.Ordinal, h.Text, h.Score)).ToList();
        }
        catch (HttpRequestException)
        {
            return []; // Knowledge unreachable → degrade gracefully
        }
    }

    private static string? BearerToken(HttpContext? http)
    {
        var header = http?.Request.Headers.Authorization.ToString();
        return header is not null && header.StartsWith("Bearer ", StringComparison.Ordinal)
            ? header["Bearer ".Length..]
            : null;
    }

    private sealed record SearchBody(string Query, int TopK);

    private sealed record SearchResult(List<Hit>? Hits);

    private sealed record Hit(Guid ChunkId, Guid DocumentId, int Ordinal, string Text, double Score);
}
