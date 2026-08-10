using Korkut.Jobs.Persistence;
using Korkut.TestSupport;
using Korkut.Contract.Documents.Storage;
using Korkut.Contract.Documents;
using Korkut.Core;
using Korkut.Organization;
using Korkut.Organization.Entities;
using System.Text;
using Korkut.Contract.Documents.OCR;
using Korkut.Documents.Services;
using Korkut.Documents.Storage;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Documents.Tests;

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
        // A fake test double's own version scheme, deliberately decoupled from
        // PlainTextExtractorService's "plaintext-vN" numbering so the two stay distinguishable
        // in the coexisting-artifacts test below regardless of how the real parser evolves.
        public string ParserVersion => "test-upper-v1";

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

    /// <summary>Same as <see cref="SeedCleanDocumentAsync"/> but for arbitrary bytes/content-type (PDF fixtures).</summary>
    private async Task<Guid> SeedCleanDocumentAsync(byte[] bytes, string filename, string contentType)
    {
        await using var seed = pg.NewContext();
        var docs = new DocumentStorageService(seed, minio.StoreService, new Clock(DateTimeOffset.UtcNow));
        var stored = await docs.StoreAsync(_office, null, new MemoryStream(bytes), filename, contentType);
        await seed.Documents.Where(x => x.Id == stored.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "clean"), default);
        return stored.Id;
    }

    [Fact]
    public async Task Extracted_spans_are_traceable_to_source_document_version_and_page()
    {
        await using var db = pg.NewContext();
        var docId = await SeedCleanDocumentAsync("Sayfa bir paragraf.\n\nIkinci paragraf.\fSayfa iki.");
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), new Clock(DateTimeOffset.UtcNow));

        var result = await svc.ExtractAsync(_office, docId);
        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.Equal(3, result.SpanCount); // 2 paragraphs on page 1, 1 on page 2

        var spans = await db.DocumentExtractions.AsNoTracking()
            .Where(x => x.DocumentId == docId).OrderBy(x => x.Page).ThenBy(x => x.SpanIndex).ToListAsync();
        Assert.All(spans, s =>
        {
            Assert.Equal(docId, s.DocumentId);
            Assert.Equal(1, s.DocumentVersion);
            Assert.Equal("plaintext-v3", s.ParserVersion);
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
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), new Clock(DateTimeOffset.UtcNow));

        var result = await svc.ExtractAsync(_office, stored.Id);
        Assert.Equal(ExtractionOutcome.SkippedNotClean, result.Outcome);
        Assert.Equal(0, await db.DocumentExtractions.CountAsync(x => x.DocumentId == stored.Id));
    }

    [Fact]
    public async Task Re_running_the_same_parser_is_idempotent()
    {
        await using var db = pg.NewContext();
        var docId = await SeedCleanDocumentAsync("Tek paragraf.");
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), new Clock(DateTimeOffset.UtcNow));

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

        await new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), clock).ExtractAsync(_office, docId);
        await new ExtractionService(db, minio.StoreService, new UpperExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), clock).ExtractAsync(_office, docId);

        var byParser = await db.DocumentExtractions.AsNoTracking()
            .Where(x => x.DocumentId == docId).GroupBy(x => x.ParserVersion)
            .Select(g => new { g.Key, Text = g.First().Text }).ToListAsync();
        Assert.Equal(2, byParser.Count); // both artifact versions coexist
        Assert.Contains(byParser, x => x is { Key: "plaintext-v3", Text: "metin" });
        Assert.Contains(byParser, x => x is { Key: "test-upper-v1", Text: "METIN" });
    }

    [Fact]
    public async Task Legal_hold_blocks_deletion()
    {
        await using var db = pg.NewContext();
        var docId = await SeedCleanDocumentAsync("delil");
        var life = new DocumentLifecycleService(db, minio.StoreService, new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), new Clock(DateTimeOffset.UtcNow));

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
        await new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), clock).ExtractAsync(_office, docId);
        var key = await db.Documents.Where(x => x.Id == docId).Select(x => x.ObjectKey).SingleAsync();

        await new DocumentLifecycleService(db, minio.StoreService, new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), clock).TombstoneAsync(_office, docId);

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

        var purged = await new DocumentLifecycleService(db, minio.StoreService, new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), new Clock(DateTimeOffset.UtcNow)).PurgeExpiredAsync(_office);
        Assert.Equal(1, purged);

        Assert.Equal("tombstoned", await db.Documents.AsNoTracking().Where(x => x.Id == expired).Select(x => x.Status).SingleAsync());
        Assert.Equal("clean", await db.Documents.AsNoTracking().Where(x => x.Id == expiredButHeld).Select(x => x.Status).SingleAsync());
        Assert.Equal("clean", await db.Documents.AsNoTracking().Where(x => x.Id == future).Select(x => x.Status).SingleAsync());
    }

    /// <summary>
    /// End-to-end regression test for PR #185 (fix/pdf-upload-null-byte-crash): a real PDF goes
    /// through StoreAsync (quarantine bucket + MinIO) → ExtractionService (PdfPig text layer) →
    /// SaveChangesAsync against real Postgres, proving the whole pipeline — not just the
    /// extractor in isolation — survives an actual PDF upload without the "invalid byte
    /// sequence for encoding UTF8: 0x00" crash the fix addresses.
    /// </summary>
    [Fact]
    public async Task Uploaded_pdf_is_extracted_and_persisted_end_to_end()
    {
        await using var db = pg.NewContext();
        var pdfBytes = PdfFixtures.MinimalOnePage("Vekaletname Ornegi");
        var docId = await SeedCleanDocumentAsync(pdfBytes, "vekaletname.pdf", "application/pdf");
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), new Clock(DateTimeOffset.UtcNow));

        var result = await svc.ExtractAsync(_office, docId);

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.True(result.SpanCount > 0);

        var spans = await db.DocumentExtractions.AsNoTracking().Where(x => x.DocumentId == docId).ToListAsync();
        Assert.Contains(spans, s => s.Text.Contains("Vekaletname"));
        Assert.All(spans, s => Assert.Equal("plaintext-v3", s.ParserVersion));
    }

    /// <summary>
    /// The exact scenario PR #185 was written for: before the fix, a PDF (or any binary file
    /// mislabeled as text) was read as raw UTF-8, embedded NUL/control bytes reached
    /// SaveChangesAsync unmodified, and Postgres rejected the insert with 22021. This asserts the
    /// full pipeline no longer crashes and instead persists clean text.
    /// </summary>
    [Fact]
    public async Task Binary_content_with_embedded_nul_bytes_does_not_crash_postgres_on_save()
    {
        await using var db = pg.NewContext();
        // Content-type says text/plain (as an unvalidated client-supplied header might), but the
        // bytes contain raw NUL/control bytes mixed with real text, as a misread binary would.
        var bytes = "sozlesme\0madde\x07 1\0devam ediyor"u8.ToArray();
        var docId = await SeedCleanDocumentAsync(bytes, "sozlesme.txt", "text/plain");
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), new Clock(DateTimeOffset.UtcNow));

        var result = await svc.ExtractAsync(_office, docId); // must not throw a Postgres exception

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        var spans = await db.DocumentExtractions.AsNoTracking().Where(x => x.DocumentId == docId).ToListAsync();
        Assert.NotEmpty(spans);
        Assert.All(spans, s => Assert.DoesNotContain('\0', s.Text));
    }

    /// <summary>
    /// A corrupt/malformed PDF (or one PdfPig can't parse — encrypted, unusually structured) must
    /// degrade to zero extracted spans rather than failing the whole extraction run or the upload.
    /// </summary>
    [Fact]
    public async Task Corrupt_pdf_extracts_zero_spans_without_failing_the_run()
    {
        await using var db = pg.NewContext();
        var garbage = "%PDF-1.4\nnot a real pdf body, no valid objects or xref"u8.ToArray();
        var docId = await SeedCleanDocumentAsync(garbage, "bozuk.pdf", "application/pdf");
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), new Clock(DateTimeOffset.UtcNow));

        var result = await svc.ExtractAsync(_office, docId);

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.Equal(0, result.SpanCount);
        Assert.Equal(0, await db.DocumentExtractions.CountAsync(x => x.DocumentId == docId));
    }

    /// <summary>PDF detection must not depend on the client-supplied content-type — a mislabeled upload is still sniffed by its %PDF magic bytes.</summary>
    [Fact]
    public async Task Pdf_uploaded_with_a_generic_content_type_is_still_detected_and_extracted()
    {
        await using var db = pg.NewContext();
        var pdfBytes = PdfFixtures.MinimalOnePage("Yanlis Etiketli Dosya");
        var docId = await SeedCleanDocumentAsync(pdfBytes, "belge", "application/octet-stream");
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), new Clock(DateTimeOffset.UtcNow));

        var result = await svc.ExtractAsync(_office, docId);

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        var spans = await db.DocumentExtractions.AsNoTracking().Where(x => x.DocumentId == docId).ToListAsync();
        Assert.Contains(spans, s => s.Text.Contains("Yanlis"));
    }

    /// <summary>
    /// End-to-end for DOCX upload: the fixture is built by MarkdownDocumentExporter (the same
    /// code real "taslak belge indir" downloads go through), so this proves the whole
    /// export → upload → extract round trip, not just the extractor in isolation.
    /// </summary>
    [Fact]
    public async Task Uploaded_docx_is_extracted_and_persisted_end_to_end()
    {
        await using var db = pg.NewContext();
        var docx = MarkdownDocumentExporter.Export("docx", "Dilekce", "# Basvuru\n\nSayin Mahkeme Baskanligina sunulmustur.");
        var docId = await SeedCleanDocumentAsync(docx.Bytes, "dilekce.docx", "application/docx");
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), new Clock(DateTimeOffset.UtcNow));

        var result = await svc.ExtractAsync(_office, docId);

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        var spans = await db.DocumentExtractions.AsNoTracking().Where(x => x.DocumentId == docId).ToListAsync();
        Assert.Contains(spans, s => s.Text.Contains("Basvuru"));
        Assert.Contains(spans, s => s.Text.Contains("Sayin Mahkeme"));
        Assert.All(spans, s => Assert.Equal("plaintext-v3", s.ParserVersion));
    }

    /// <summary>End-to-end for UDF (UYAP) upload — the format this fix was specifically requested for.</summary>
    [Fact]
    public async Task Uploaded_udf_is_extracted_and_persisted_end_to_end()
    {
        await using var db = pg.NewContext();
        var udf = MarkdownDocumentExporter.Export("udf", "Vekaletname", "# Madde 1\n\nIsbu vekaletname ile yetki verilmistir.");
        var docId = await SeedCleanDocumentAsync(udf.Bytes, "vekaletname.udf", "application/udf");
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), new Clock(DateTimeOffset.UtcNow));

        var result = await svc.ExtractAsync(_office, docId);

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        var spans = await db.DocumentExtractions.AsNoTracking().Where(x => x.DocumentId == docId).ToListAsync();
        Assert.Contains(spans, s => s.Text.Contains("Madde 1"));
        Assert.Contains(spans, s => s.Text.Contains("Isbu vekaletname"));
    }

    /// <summary>Legacy .doc is accepted end-to-end (stored, marked clean) even though text extraction isn't implemented for it yet — no crash, just zero spans.</summary>
    [Fact]
    public async Task Uploaded_legacy_doc_does_not_fail_the_extraction_run()
    {
        await using var db = pg.NewContext();
        byte[] ole2 = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0];
        var docId = await SeedCleanDocumentAsync(ole2, "eski-sozlesme.doc", "application/doc");
        var svc = new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), new IndexOutboxService(db, new Clock(DateTimeOffset.UtcNow)), new Clock(DateTimeOffset.UtcNow));

        var result = await svc.ExtractAsync(_office, docId);

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.Equal(0, result.SpanCount);
    }
}
