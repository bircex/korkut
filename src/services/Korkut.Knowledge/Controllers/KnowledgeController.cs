using Korkut.Core;
using System.ComponentModel.DataAnnotations;
using Korkut.Application.Knowledge;
using Korkut.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Knowledge.Controllers;

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
/// The internal Knowledge (RAG) surface: ingest a document's text into the vector index, and
/// search it. Authenticated with the same access token as the other hosts and tenant-scoped (RLS
/// on the Knowledge DB) — the office comes from the token, never the body. Not publicly routed;
/// the gateway keeps it in-cluster and the Agent reaches it for retrieval. When no embedding
/// backend is configured the embedder raises ModelUnavailableException, which the shared handler
/// maps to 503. Ingest and delete need matters.write; search needs matters.read.
/// </summary>
[ApiController]
public sealed class KnowledgeController(IIngestionService ingestion, IRetrievalService retrieval) : ControllerBase
{
    [HttpPost("/ingest")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Ingest([FromBody] IngestRequest body, CancellationToken ct)
    {
        var chunks = await ingestion.IngestAsync(body.DocumentId, body.Text ?? string.Empty, ct);
        return Ok(new IngestResponse(body.DocumentId, chunks));
    }

    [HttpPost("/search")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Search([FromBody] SearchRequest body, CancellationToken ct)
    {
        var hits = await retrieval.SearchAsync(body.Query!.Trim(), body.TopK ?? RetrievalService.DefaultTopK, ct);
        return Ok(new SearchResponse(
            hits.Select(h => new SearchHitResponse(h.ChunkId, h.DocumentId, h.Ordinal, h.Text, h.Score)).ToList()));
    }

    [HttpDelete("/documents/{documentId:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Delete(Guid documentId, CancellationToken ct)
    {
        // Re-ingesting empty text clears every chunk of the document from the index.
        await ingestion.IngestAsync(documentId, string.Empty, ct);
        return Ok(new { ok = true });
    }
}
