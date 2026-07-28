using Korkut.Contract.Documents;
using Korkut.Core;
using Korkut.Organization;
using Korkut.Organization.Entities;
using System.Text;
using Korkut.Application.Documents;
using Korkut.Application.Documents.OCR;
using Korkut.Application.Documents.Storage;
using Korkut.Infrastructure.Documents;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// Document extraction + lifecycle over real MinIO + real PostgreSQL (KDM-027):
/// provenance, immutable-per-parser versioning, retry idempotency, clean-only
/// processing, legal hold, and retention purge.
/// </summary>
public class ExtractionTests(PostgresFixture pg, MinioFixture minio)
    : IClassFixture<PostgresFixture>, IClassFixture<MinioFixture>, IAsyncLifetime
{
    private Guid _office;

    private sealed class Clock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    /// <summary>A second parser with a distinct version — simulates a parser upgrade.</summary>
    private sealed class UpperExtractorService : ITextExtractorService
    {
        public string ParserVersion => "plaintext-v2";

        public Task<IReadOnlyList<TextExtractorServiceResult>> ExtractAsync(Stream content, string contentType, CancellationToken ct = default)
        {
            using var r = new StreamReader(content, Encoding.UTF8, leaveOpen: true);
            var text = r.ReadToEnd().Trim().ToUpperInvariant();
            IReadOnlyList<TextExtractorServiceResult> spans = [new TextExtractorServiceResult(1, 0, text)];
            return Task.FromResult(spans);
        }
    }

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _office = Guid.CreateVersion7();
        await using var db = pg.NewContext();
        db.Offices.Add(new Office(_office, "O", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Ingests a clean text document and returns its id. Uses its own context so the
    /// caller's context starts with no stale tracking of the new row.
    /// </summary>
    private async Task<Guid> SeedCleanDocumentAsync(string body)
    {
        await using var seed = pg.NewContext();
        var docs = new DocumentStorageService(seed, minio.StoreService, new Clock(DateTimeOffset.UtcNow));
        var stored = await docs.StoreAsync(_office, null, new MemoryStream(Encoding.UTF8.GetBytes(body)), "brief.txt", "text/plain");
        await seed.Documents.Where(x => x.Id == stored.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "clean"), default);
        return stored.Id;
    }

    [Fact]
    public async Task Extracted_spans_are_traceable_to_source_document_version_and_page()
    {
        await using var db = pg.NewContext();
        var docId = await SeedCleanDocumentAsync("Sayfa bir paragraf.\n\nIkinci paragraf.\fSayfa iki.");
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new Clock(DateTimeOffset.UtcNow));

        var result = await svc.ExtractAsync(_office, docId);
        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.Equal(3, result.SpanCount); // 2 paragraphs on page 1, 1 on page 2

        var spans = await db.DocumentExtractions.AsNoTracking()
            .Where(x => x.DocumentId == docId).OrderBy(x => x.Page).ThenBy(x => x.SpanIndex).ToListAsync();
        Assert.All(spans, s =>
        {
            Assert.Equal(docId, s.DocumentId);
            Assert.Equal(1, s.DocumentVersion);
            Assert.Equal("plaintext-v1", s.ParserVersion);
            Assert.NotEmpty(s.SourceChecksumSha256);
            Assert.NotEmpty(s.SourceObjectKey);
        });
        Assert.Equal([1, 1, 2], spans.Select(s => s.Page));
    }

    [Fact]
    public async Task Only_clean_documents_are_processed()
    {
        await using var db = pg.NewContext();
        var docs = new DocumentStorageService(db, minio.StoreService, new Clock(DateTimeOffset.UtcNow));
        // Left in quarantine — never promoted to clean.
        var stored = await docs.StoreAsync(_office, null, new MemoryStream("x"u8.ToArray()), "q.txt", "text/plain");
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new Clock(DateTimeOffset.UtcNow));

        var result = await svc.ExtractAsync(_office, stored.Id);
        Assert.Equal(ExtractionOutcome.SkippedNotClean, result.Outcome);
        Assert.Equal(0, await db.DocumentExtractions.CountAsync(x => x.DocumentId == stored.Id));
    }

    [Fact]
    public async Task Re_running_the_same_parser_is_idempotent()
    {
        await using var db = pg.NewContext();
        var docId = await SeedCleanDocumentAsync("Tek paragraf.");
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new Clock(DateTimeOffset.UtcNow));

        var first = await svc.ExtractAsync(_office, docId);
        var second = await svc.ExtractAsync(_office, docId); // retry
        Assert.Equal(ExtractionOutcome.Extracted, first.Outcome);
        Assert.Equal(ExtractionOutcome.AlreadyExtracted, second.Outcome);
        Assert.Equal(1, await db.DocumentExtractions.CountAsync(x => x.DocumentId == docId)); // no duplicate
    }

    [Fact]
    public async Task A_new_parser_version_adds_a_new_artifact_without_overwriting_the_old_one()
    {
        await using var db = pg.NewContext();
        var docId = await SeedCleanDocumentAsync("metin");
        var clock = new Clock(DateTimeOffset.UtcNow);

        await new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), clock).ExtractAsync(_office, docId);
        await new ExtractionService(db, minio.StoreService, new UpperExtractorService(), clock).ExtractAsync(_office, docId);

        var byParser = await db.DocumentExtractions.AsNoTracking()
            .Where(x => x.DocumentId == docId).GroupBy(x => x.ParserVersion)
            .Select(g => new { g.Key, Text = g.First().Text }).ToListAsync();
        Assert.Equal(2, byParser.Count); // both artifact versions coexist
        Assert.Contains(byParser, x => x is { Key: "plaintext-v1", Text: "metin" });
        Assert.Contains(byParser, x => x is { Key: "plaintext-v2", Text: "METIN" });
    }

    [Fact]
    public async Task Legal_hold_blocks_deletion()
    {
        await using var db = pg.NewContext();
        var docId = await SeedCleanDocumentAsync("delil");
        var life = new DocumentLifecycleService(db, minio.StoreService, new Clock(DateTimeOffset.UtcNow));

        await life.SetLegalHoldAsync(_office, docId, held: true);
        // Legal hold is an absolute veto, and the caller is told so (409, not a crash).
        await Assert.ThrowsAsync<ConflictException>(() => life.TombstoneAsync(_office, docId));

        var doc = await db.Documents.AsNoTracking().SingleAsync(x => x.Id == docId);
        Assert.Equal("clean", doc.Status); // untouched
    }

    [Fact]
    public async Task Tombstone_purges_bytes_and_artifacts_but_keeps_the_metadata_row()
    {
        await using var db = pg.NewContext();
        var docId = await SeedCleanDocumentAsync("silinecek");
        var clock = new Clock(DateTimeOffset.UtcNow);
        await new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), clock).ExtractAsync(_office, docId);
        var key = await db.Documents.Where(x => x.Id == docId).Select(x => x.ObjectKey).SingleAsync();

        await new DocumentLifecycleService(db, minio.StoreService, clock).TombstoneAsync(_office, docId);

        var doc = await db.Documents.AsNoTracking().SingleAsync(x => x.Id == docId);
        Assert.Equal("tombstoned", doc.Status);
        Assert.NotNull(doc.TombstonedAt);
        Assert.Equal(0, await db.DocumentExtractions.CountAsync(x => x.DocumentId == docId)); // artifacts gone
        await Assert.ThrowsAnyAsync<Exception>(() => minio.StoreService.GetAsync(ObjectBuckets.Quarantine, key)); // bytes gone
    }

    [Fact]
    public async Task Retention_purge_tombstones_expired_documents_but_never_held_ones()
    {
        await using var db = pg.NewContext();
        var expired = await SeedCleanDocumentAsync("a");
        var expiredButHeld = await SeedCleanDocumentAsync("b");
        var future = await SeedCleanDocumentAsync("c");

        var past = DateTimeOffset.UtcNow.AddDays(-1);
        var ahead = DateTimeOffset.UtcNow.AddDays(30);
        await db.Documents.Where(x => x.Id == expired).ExecuteUpdateAsync(s => s.SetProperty(x => x.RetentionUntil, past), default);
        await db.Documents.Where(x => x.Id == expiredButHeld)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RetentionUntil, past).SetProperty(x => x.LegalHold, true), default);
        await db.Documents.Where(x => x.Id == future).ExecuteUpdateAsync(s => s.SetProperty(x => x.RetentionUntil, ahead), default);

        var purged = await new DocumentLifecycleService(db, minio.StoreService, new Clock(DateTimeOffset.UtcNow)).PurgeExpiredAsync(_office);
        Assert.Equal(1, purged);

        Assert.Equal("tombstoned", await db.Documents.AsNoTracking().Where(x => x.Id == expired).Select(x => x.Status).SingleAsync());
        Assert.Equal("clean", await db.Documents.AsNoTracking().Where(x => x.Id == expiredButHeld).Select(x => x.Status).SingleAsync());
        Assert.Equal("clean", await db.Documents.AsNoTracking().Where(x => x.Id == future).Select(x => x.Status).SingleAsync());
    }
}
