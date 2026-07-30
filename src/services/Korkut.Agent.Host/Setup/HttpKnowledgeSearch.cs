using Korkut.Core;
using Korkut.Knowledge;
using System.Net.Http.Json;
using System.Text.Json;

namespace Korkut.Agent.Host.Setup;

/// <summary>
/// The retrieval adapter the Agent uses to ground a chat turn: it POSTs the internal
/// Korkut.Knowledge <c>/search</c> endpoint (KORKUT_KNOWLEDGE_URL), forwarding the caller's auth
/// so Knowledge derives the office (RLS) from the token — the office is never sent in the body.
/// When Knowledge is unconfigured or unreachable (e.g. no embedding backend → 503) it returns an
/// empty list, so chat degrades gracefully to ungrounded rather than failing.
/// </summary>
internal sealed class HttpKnowledgeSearch(
    IHttpClientFactory httpFactory, IEnvironment env, IHttpContextAccessor accessor) : IKnowledgeSearchService
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, CancellationToken ct = default)
    {
        var baseUrl = env.Get("KORKUT_KNOWLEDGE_URL")?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return [];
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/search")
            {
                Content = JsonContent.Create(new { Query = query, TopK = topK }, options: Json),
            };

            // Forward the caller's session so Knowledge authenticates + tenant-scopes the search.
            var incoming = accessor.HttpContext?.Request;
            if (incoming is not null)
            {
                if (incoming.Headers.TryGetValue("Cookie", out var cookie))
                {
                    req.Headers.TryAddWithoutValidation("Cookie", cookie.ToArray());
                }

                if (incoming.Headers.TryGetValue("Authorization", out var auth))
                {
                    req.Headers.TryAddWithoutValidation("Authorization", auth.ToArray());
                }
            }

            var response = await httpFactory.CreateClient("ai").SendAsync(req, ct);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var body = await response.Content.ReadFromJsonAsync<SearchResponseDto>(Json, ct);
            return body?.Hits is { } hits
                ? hits.Select(h => new SearchHit(h.ChunkId, h.DocumentId, h.Ordinal, h.Text, h.Score)).ToList()
                : [];
        }
        catch
        {
            // Retrieval is best-effort grounding: never let it fail the chat turn.
            return [];
        }
    }

    private sealed record SearchResponseDto(List<HitDto>? Hits);

    private sealed record HitDto(Guid ChunkId, Guid DocumentId, int Ordinal, string Text, double Score);
}
