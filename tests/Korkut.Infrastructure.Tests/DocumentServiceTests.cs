using Korkut.TestSupport;
using Korkut.Contract.Documents.Storage;
using Korkut.Infrastructure.Documents;
using Korkut.Organization;
using Korkut.Organization.Entities;
using System.Security.Cryptography;
using System.Text;
using Korkut.Contract.Documents;
using Korkut.Infrastructure.Storage;
using Korkut.Infrastructure.Persistence;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Tests;

/// <summary>Document storage over real MinIO + real PostgreSQL.</summary>
public class DocumentServiceTests(PostgresFixture pg, MinioFixture minio)
    : IClassFixture<PostgresFixture>, IClassFixture<MinioFixture>, IAsyncLifetime
{
    private Guid _office;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
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

    private DocumentStorageService Service(KorkutDbContext db) => new(db, minio.StoreService, new Clock());

    [Fact]
    public async Task Stored_document_round_trips_with_a_matching_checksum_and_opaque_key()
    {
        var bytes = Encoding.UTF8.GetBytes("gizli dava belgesi — Ahmet Yılmaz");
        var expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        StoredDocument stored;
        await using (var db = pg.NewContext())
        {
            stored = await Service(db).StoreAsync(_office, null, new MemoryStream(bytes), "gizli-dava.pdf", "application/pdf");
        }

        Assert.Equal(expected, stored.ChecksumSha256);
        // Opaque key: no filename, no PII.
        Assert.DoesNotContain("gizli", stored.ObjectKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ahmet", stored.ObjectKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_office.ToString(), stored.ObjectKey, StringComparison.OrdinalIgnoreCase);

        // Bytes are actually in MinIO (quarantine) and match.
        await using var download = await minio.StoreService.GetAsync(ObjectBuckets.Quarantine, stored.ObjectKey);
        using var ms = new MemoryStream();
        await download.CopyToAsync(ms);
        Assert.Equal(bytes, ms.ToArray());

        // Filename lives in the DB, never the key.
        await using var check = pg.NewContext();
        var doc = await check.Documents.AsNoTracking().SingleAsync(x => x.Id == stored.Id);
        Assert.Equal("gizli-dava.pdf", doc.OriginalFilename);
        Assert.Equal("quarantine", doc.Status);
    }

    [Fact]
    public async Task Presigned_url_is_time_limited_and_points_at_the_object()
    {
        StoredDocument stored;
        await using (var db = pg.NewContext())
        {
            stored = await Service(db).StoreAsync(_office, null, new MemoryStream([1, 2, 3]), "x.bin", "application/octet-stream");
            // Presign is only allowed once a document is proven clean (KDM-026).
            await db.Documents.Where(x => x.Id == stored.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "clean"), default);
        }

        await using var db2 = pg.NewContext();
        var url = await Service(db2).PresignAsync(_office, stored.Id, TimeSpan.FromMinutes(2));
        Assert.Contains(stored.ObjectKey, url, StringComparison.Ordinal);
        // Presigned URLs carry an expiry parameter (short-lived).
        Assert.Contains("X-Amz-Expires=", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Presign_across_offices_is_blocked()
    {
        StoredDocument stored;
        await using (var db = pg.NewContext())
        {
            stored = await Service(db).StoreAsync(_office, null, new MemoryStream([9]), "x.bin", "application/octet-stream");
        }

        var otherOffice = Guid.CreateVersion7();
        await using var db2 = pg.NewContext();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service(db2).PresignAsync(otherOffice, stored.Id, TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public async Task Reconciliation_reports_a_db_row_whose_object_was_deleted()
    {
        StoredDocument stored;
        await using (var db = pg.NewContext())
        {
            stored = await Service(db).StoreAsync(_office, null, new MemoryStream([1]), "x.bin", "application/octet-stream");
        }

        // Delete the object out from under the DB row -> orphan.
        await minio.StoreService.RemoveAsync(ObjectBuckets.Quarantine, stored.ObjectKey);

        await using var db2 = pg.NewContext();
        var report = await Service(db2).ReconcileAsync(ObjectBuckets.Quarantine);
        Assert.Contains(stored.Id, report.DbRowsMissingObject);
    }
}
