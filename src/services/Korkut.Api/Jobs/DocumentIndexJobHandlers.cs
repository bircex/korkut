using System.Text.Json;
using Korkut.Contract.Documents;
using Korkut.Core;
using Korkut.Documents.Services;
using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Entities;
using Korkut.Knowledge;

namespace Korkut.Api.Jobs;

/// <summary>
/// Puts a document's extracted text into the search index the assistant grounds its answers on.
/// Queued by extraction once a clean upload's text is read; it needs no caller, only the office
/// from the message. An install without an embedding backend has no index to fill, so the job
/// completes without one rather than failing and retrying for nothing.
/// </summary>
public sealed partial class DocumentIndexJobHandler(
    DocumentTextService texts, IIngestionService ingestion, ILogger<DocumentIndexJobHandler> logger) : IJobHandler
{
    public string MessageType => DocumentIndexEventTypes.Index;

    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var (documentId, version) = DocumentIndexPayload.Read(message);
        var text = await texts.ReadForIndexAsync(message.OfficeId, documentId, version, cancellationToken);
        if (text is null)
        {
            return; // deleted, no longer clean, or superseded by a newer version
        }

        try
        {
            await ingestion.IngestAsync(message.OfficeId, documentId, text, cancellationToken);
        }
        catch (ModelUnavailableException ex)
        {
            NotIndexed(logger, documentId, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Document {DocumentId} not indexed: {Reason}")]
    private static partial void NotIndexed(ILogger logger, Guid documentId, string reason);
}

/// <summary>Takes a deleted document out of the search index.</summary>
public sealed class DocumentDeindexJobHandler(IIngestionService ingestion) : IJobHandler
{
    public string MessageType => DocumentIndexEventTypes.Deindex;

    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var (documentId, _) = DocumentIndexPayload.Read(message);
        // Ingesting empty text clears every chunk; it never calls the embedder, so it works with
        // or without an embedding backend.
        await ingestion.IngestAsync(message.OfficeId, documentId, string.Empty, cancellationToken);
    }
}

/// <summary>The document fields of an index/de-index event. The office always comes from the
/// message's trusted metadata, never from the payload.</summary>
internal static class DocumentIndexPayload
{
    public static (Guid DocumentId, int Version) Read(OutboxMessage message)
    {
        using var payload = JsonDocument.Parse(message.Payload);
        var root = payload.RootElement;
        return (root.GetProperty("document_id").GetGuid(), root.GetProperty("version").GetInt32());
    }
}
