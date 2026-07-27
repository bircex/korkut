using Korkut.Application.Documents;
using Korkut.Application.Documents.OCR;
using Korkut.Application.Documents.Storage;
using Korkut.Documents;
using Korkut.Infrastructure.Persistence;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Documents;

/// <summary>Outcome of an extraction run (KDM-027).</summary>
public enum ExtractionOutcome
{
    /// <summary>Spans were produced and persisted for this parser version.</summary>
    Extracted,

    /// <summary>Artifacts for this (document version, parser version) already existed — no-op.</summary>
    AlreadyExtracted,

    /// <summary>The source document was not in the <c>clean</c> state.</summary>
    SkippedNotClean,
}

public sealed record ExtractionResult(ExtractionOutcome Outcome, int SpanCount);

/// <summary>
/// Turns a clean document's bytes into immutable, provenance-tracked text spans
/// (KDM-027). Enforces three invariants:
/// <list type="bullet">
/// <item>Only <c>clean</c> documents are processed — pending/rejected/quarantine/tombstoned are skipped.</item>
/// <item>A parser change (new <see cref="ITextExtractorService.ParserVersion"/>) writes a NEW
/// artifact set and never overwrites the old one.</item>
/// <item>Re-running the same parser over the same document version is idempotent
/// (retry-safe): the unique provenance index turns duplicate inserts into a no-op.</item>
/// </list>
/// </summary>
public sealed class ExtractionService(
    KorkutDbContext db,
    IObjectStoreService storeService,
    ITextExtractorService extractorService,
    IClock clock)
{
    public async Task<ExtractionResult> ExtractAsync(Guid officeId, Guid documentId, CancellationToken ct = default)
    {
        var doc = await db.Documents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == documentId, ct)
            ?? throw new InvalidOperationException("Document not found.");

        // Worker may only process objects proven clean — never pending/quarantine/rejected/tombstoned.
        if (!doc.IsDownloadable)
        {
            return new ExtractionResult(ExtractionOutcome.SkippedNotClean, 0);
        }

        // Immutable-per-parser: if artifacts already exist for this document version +
        // parser, this is a retry — do nothing (idempotent).
        var alreadyDone = await db.DocumentExtractions.AsNoTracking().AnyAsync(
            x => x.OfficeId == officeId
                && x.DocumentId == documentId
                && x.DocumentVersion == doc.Version
                && x.ParserVersion == extractorService.ParserVersion,
            ct);
        if (alreadyDone)
        {
            return new ExtractionResult(ExtractionOutcome.AlreadyExtracted, 0);
        }

        await using var content = await storeService.GetAsync(doc.Bucket, doc.ObjectKey, ct);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var spans = await extractorService.ExtractAsync(buffer, doc.ContentType, ct);
        var now = clock.UtcNow;
        foreach (var span in spans)
        {
            db.DocumentExtractions.Add(new DocumentExtraction(
                officeId, documentId, doc.Version, extractorService.ParserVersion,
                span.Page, span.Index, span.Text, doc.ChecksumSha256, doc.ObjectKey, now));
        }

        await db.SaveChangesAsync(ct);

        // The projection is search-ready — enqueue an index event (idempotent per version).
        await new IndexOutboxService(db, clock).EnqueueIndexAsync(officeId, documentId, doc.Version, ct);
        return new ExtractionResult(ExtractionOutcome.Extracted, spans.Count);
    }
}
