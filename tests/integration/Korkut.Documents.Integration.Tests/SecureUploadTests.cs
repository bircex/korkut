using System.IO.Compression;
using System.Text;
using Korkut.Audit;
using Korkut.Contract.Upload.Malware;
using Korkut.Contract.Upload.SafeUpload;
using Korkut.Contract.Upload.Validator;
using Korkut.Core;
using Korkut.Documents.Services;
using Korkut.Documents.Storage;
using Korkut.Infrastructure.Persistence;
using Korkut.Organization.Entities;
using Korkut.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Documents.Integration.Tests;

public class SecureUploadTests(PostgresFixture pg, ObjectStoreFixture store)
    : IClassFixture<PostgresFixture>, IClassFixture<ObjectStoreFixture>, IAsyncLifetime
{
    private Guid _office;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class BrokenScannerService : IMalwareScanService
    {
        public Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken ct = default)
            => throw new InvalidOperationException("scanner down");
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

    private SafeUploadService Pipeline(KorkutDbContext db, IMalwareScanService? scanner = null)
    {
        var docs = new DocumentStorageService(db, store.StoreService, new Clock());
        return new SafeUploadService(db, docs, store.StoreService, new UploadValidatorService(),
            scanner ?? new MalwareScannerService(), new AuditLog(db, new Clock()));
    }

    private static MemoryStream Pdf(byte[]? extra = null)
    {
        var ms = new MemoryStream();
        ms.Write("%PDF-1.7\n"u8.ToArray());
        if (extra is not null)
        {
            ms.Write(extra);
        }

        ms.Write("\n%%EOF"u8.ToArray());
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task Clean_pdf_is_promoted_to_clean_and_becomes_downloadable()
    {
        await using var db = pg.NewContext();
        var outcome = await Pipeline(db).IngestAsync(_office, null, Pdf(), "sozlesme.pdf");
        Assert.Equal(SafeUploadStatus.Clean, outcome.Status);

        var doc = await db.Documents.AsNoTracking().SingleAsync(x => x.Id == outcome.DocumentId);
        Assert.Equal("clean", doc.Status);
        Assert.Equal(ObjectBuckets.Clean, doc.Bucket);
        // Now downloadable.
        var url = await new DocumentStorageService(db, store.StoreService, new Clock()).PresignAsync(_office, doc.Id, TimeSpan.FromMinutes(1));
        Assert.StartsWith(ObjectUrlSigner.PathPrefix, url, StringComparison.Ordinal);
        Assert.Equal((ObjectBuckets.Clean, doc.ObjectKey), store.Signer.Verify(url[ObjectUrlSigner.PathPrefix.Length..]));
    }

    [Fact]
    public async Task Eicar_is_rejected_as_malware_and_not_downloadable()
    {
        var eicar = Encoding.ASCII.GetBytes(@"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
        await using var db = pg.NewContext();
        var outcome = await Pipeline(db).IngestAsync(_office, null, Pdf(eicar), "virus.pdf");

        Assert.Equal(SafeUploadStatus.Rejected, outcome.Status);
        Assert.Equal(UploadValidatorRejection.Malware, outcome.ValidatorRejection);
        var doc = await db.Documents.AsNoTracking().SingleAsync(x => x.Id == outcome.DocumentId);
        Assert.Equal("rejected", doc.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new DocumentStorageService(db, store.StoreService, new Clock()).PresignAsync(_office, doc.Id, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task Spoofed_mime_executable_named_pdf_is_rejected_by_magic_bytes()
    {
        // Client claims .pdf but the bytes are an MZ (PE/exe) header.
        var exe = new MemoryStream([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00]);
        await using var db = pg.NewContext();
        var outcome = await Pipeline(db).IngestAsync(_office, null, exe, "document.pdf");
        Assert.Equal(SafeUploadStatus.Rejected, outcome.Status);
        Assert.Equal(UploadValidatorRejection.ContentTypeMismatch, outcome.ValidatorRejection);
    }

    [Theory]
    [InlineData("dilekce.udf")]
    [InlineData("sozlesme.doc")]
    [InlineData("not.txt")]
    public async Task Udf_doc_and_txt_uploads_are_no_longer_rejected_at_the_gate(string filename)
    {
        // Before this fix, UploadValidatorService only allowlisted .pdf/.png/.jpg/.jpeg/
        // .docx/.xlsx/.zip — .udf/.doc/.txt were rejected with DisallowedExtension before ever
        // reaching extraction, regardless of how well PlainTextExtractorService supported them.
        var ext = Path.GetExtension(filename);
        var bytes = ext switch
        {
            ".udf" => MarkdownDocumentExporter.Export("udf", "Dilekce", "Sayin Mahkeme Baskanligina.").Bytes,
            ".doc" => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0],
            _ => "Duz metin belge."u8.ToArray(),
        };

        await using var db = pg.NewContext();
        var outcome = await Pipeline(db).IngestAsync(_office, null, new MemoryStream(bytes), filename);

        Assert.Equal(SafeUploadStatus.Clean, outcome.Status);
        var doc = await db.Documents.AsNoTracking().SingleAsync(x => x.Id == outcome.DocumentId);
        Assert.Equal("clean", doc.Status);
    }

    [Fact]
    public async Task Pdf_renamed_to_udf_is_still_rejected_by_magic_bytes()
    {
        // .udf shares the zip signature with .docx/.xlsx/.zip, but a PDF's bytes match none of
        // them — the per-extension magic-byte check still applies to the new extension.
        await using var db = pg.NewContext();
        var outcome = await Pipeline(db).IngestAsync(_office, null, Pdf(), "sahte.udf");
        Assert.Equal(SafeUploadStatus.Rejected, outcome.Status);
        Assert.Equal(UploadValidatorRejection.ContentTypeMismatch, outcome.ValidatorRejection);
    }

    [Fact]
    public async Task Binary_content_renamed_to_txt_is_rejected()
    {
        // .txt has no magic bytes, so "the bytes decide the type" instead means: reject content
        // that is obviously not text (a NUL byte early on, or invalid UTF-8).
        var binary = new MemoryStream([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00]);
        await using var db = pg.NewContext();
        var outcome = await Pipeline(db).IngestAsync(_office, null, binary, "notepad.txt");
        Assert.Equal(SafeUploadStatus.Rejected, outcome.Status);
        Assert.Equal(UploadValidatorRejection.ContentTypeMismatch, outcome.ValidatorRejection);
    }

    [Theory]
    [InlineData("../../etc/passwd", UploadValidatorRejection.DisallowedExtension)] // leaf "passwd" has no allowed ext
    [InlineData("x\0.pdf", UploadValidatorRejection.UnsafeFilename)]                // null byte
    public async Task Malicious_filenames_are_rejected(string filename, UploadValidatorRejection expected)
    {
        await using var db = pg.NewContext();
        var outcome = await Pipeline(db).IngestAsync(_office, null, Pdf(), filename);
        Assert.Equal(SafeUploadStatus.Rejected, outcome.Status);
        Assert.Equal(expected, outcome.ValidatorRejection);
    }

    [Fact]
    public async Task Path_components_are_stripped_from_the_stored_filename()
    {
        // A traversal-style path is neutralised to its leaf — never stored with separators.
        await using var db = pg.NewContext();
        var outcome = await Pipeline(db).IngestAsync(_office, null, Pdf(), "..\\..\\windows\\system32\\x.pdf");
        Assert.Equal(SafeUploadStatus.Clean, outcome.Status);
        var doc = await db.Documents.AsNoTracking().SingleAsync(x => x.Id == outcome.DocumentId);
        Assert.Equal("x.pdf", doc.OriginalFilename);
        Assert.DoesNotContain('/', doc.OriginalFilename);
        Assert.DoesNotContain('\\', doc.OriginalFilename);
    }

    [Fact]
    public async Task Decompression_bomb_archive_is_rejected()
    {
        // A zip whose single entry expands ~1000x — over the ratio guard.
        var zipBytes = new MemoryStream();
        using (var zip = new ZipArchive(zipBytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            var e = zip.CreateEntry("big.txt", CompressionLevel.SmallestSize);
            await using var s = e.Open();
            await s.WriteAsync(new byte[2_000_000]); // 2MB of zeros -> tiny compressed
        }

        zipBytes.Position = 0;
        await using var db = pg.NewContext();
        var outcome = await Pipeline(db).IngestAsync(_office, null, zipBytes, "archive.zip");
        Assert.Equal(SafeUploadStatus.Rejected, outcome.Status);
        Assert.Equal(UploadValidatorRejection.ArchiveTooDeepOrLarge, outcome.ValidatorRejection);
    }

    [Fact]
    public async Task A_broken_scanner_leaves_the_upload_pending_never_clean()
    {
        await using var db = pg.NewContext();
        var outcome = await Pipeline(db, new BrokenScannerService()).IngestAsync(_office, null, Pdf(), "x.pdf");

        Assert.Equal(SafeUploadStatus.Pending, outcome.Status); // fail-closed
        var doc = await db.Documents.AsNoTracking().SingleAsync(x => x.Id == outcome.DocumentId);
        Assert.Equal("quarantine", doc.Status);
        // Still not downloadable.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new DocumentStorageService(db, store.StoreService, new Clock()).PresignAsync(_office, doc.Id, TimeSpan.FromMinutes(1)));
    }
}
