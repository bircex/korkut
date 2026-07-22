using System.IO.Compression;
using System.Text;
using Korkut.Application.Documents;
using Korkut.Application.Documents.Storage;
using Korkut.Application.Upload;
using Korkut.Application.Upload.Malware;
using Korkut.Application.Upload.SafeUpload;
using Korkut.Application.Upload.Validator;
using Korkut.Infrastructure.Documents;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Tests;

public class SecureUploadTests(PostgresFixture pg, MinioFixture minio)
    : IClassFixture<PostgresFixture>, IClassFixture<MinioFixture>, IAsyncLifetime
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
        db.Offices.Add(new OfficeEntity { Id = _office, Name = "O", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private SafeUploadService Pipeline(KorkutDbContext db, IMalwareScanService? scanner = null)
    {
        var docs = new DocumentService(db, minio.StoreService, new Clock());
        return new SafeUploadService(db, docs, minio.StoreService, new UploadValidatorService(),
            scanner ?? new MalwareScannerService(), new EfAuditLog(db, new Clock()));
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
        var url = await new DocumentService(db, minio.StoreService, new Clock()).PresignAsync(_office, doc.Id, TimeSpan.FromMinutes(1));
        Assert.Contains(doc.ObjectKey, url, StringComparison.Ordinal);
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
            () => new DocumentService(db, minio.StoreService, new Clock()).PresignAsync(_office, doc.Id, TimeSpan.FromMinutes(1)));
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
            () => new DocumentService(db, minio.StoreService, new Clock()).PresignAsync(_office, doc.Id, TimeSpan.FromMinutes(1)));
    }
}
