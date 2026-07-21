using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Application.Ai;
using Korkut.Application.Features.Knowledge;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence.Knowledge;
using Korkut.Web.Errors;
using Korkut.Web.Tenancy;

namespace Korkut.Knowledge.Endpoints;

public sealed record IngestRequest
{
    [Required]
    public Guid DocumentId { get; init; }

    /// <summary>The document's extracted plain text; empty clears the document from the index.</summary>
    public string? Text { get; init; }
}

public sealed record IngestResponse(Guid DocumentId, int Chunks);

public sealed record SearchRequest
{
    [Required, MaxLength(4000)]
    public string? Query { get; init; }

    [Range(1, RetrievalService.MaxTopK)]
    public int? TopK { get; init; }
}

public sealed record SearchHitResponse(Guid ChunkId, Guid DocumentId, int Ordinal, string Text, double Score);

public sealed record SearchResponse(IReadOnlyList<SearchHitResponse> Hits);

/// <summary>
/// The internal Knowledge (RAG) surface: ingest a document's text into the vector index and
/// search it. Authenticated with the same access token as the other hosts, tenant-scoped (RLS on
/// the Knowledge DB). Not publicly routed — the gateway keeps it in-cluster; the Agent reaches it
/// for retrieval. Returns 503 when no embedding backend is configured. Ingest/delete require
/// matters.write; search requires matters.read.
/// </summary>
public static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/ingest", async (IngestRequest body, ClaimsPrincipal user, KnowledgeDbContext db, IIngestionService ingestion, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                try
                {
                    // The tenant scope pins RLS for the whole chunk→embed→store unit (the embedding
                    // call runs inside it, mirroring how the Agent holds its scope across the LLM call).
                    var chunks = await user.InTenantScopeAsync(db, o => ingestion.IngestAsync(o, body.DocumentId, body.Text ?? string.Empty, ct), ct);
                    return Results.Ok(new IngestResponse(body.DocumentId, chunks));
                }
                catch (ModelUnavailableException ex)
                {
                    return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .RequireAuthorization(Permissions.MattersWrite);

        app.MapPost("/search", async (SearchRequest body, ClaimsPrincipal user, KnowledgeDbContext db, IRetrievalService retrieval, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                try
                {
                    var hits = await user.InTenantScopeAsync(db, o =>
                        retrieval.SearchAsync(o, body.Query!.Trim(), body.TopK ?? RetrievalService.DefaultTopK, ct), ct);
                    return Results.Ok(new SearchResponse(
                        hits.Select(h => new SearchHitResponse(h.ChunkId, h.DocumentId, h.Ordinal, h.Text, h.Score)).ToList()));
                }
                catch (ModelUnavailableException ex)
                {
                    return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .RequireAuthorization(Permissions.MattersRead);

        app.MapDelete("/documents/{documentId:guid}", async (Guid documentId, ClaimsPrincipal user, KnowledgeDbContext db, IIngestionService ingestion, CancellationToken ct) =>
            {
                // Re-ingesting empty text clears every chunk of the document from the index.
                await user.InTenantScopeAsync(db, o => ingestion.IngestAsync(o, documentId, string.Empty, ct), ct);
                return Results.Ok(new { ok = true });
            })
            .RequireAuthorization(Permissions.MattersWrite);

        return app;
    }
}
