using System.Security.Cryptography;
using Korkut.Application.Documents;
using Korkut.Application.Documents.Storage;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.Ids;
using Korkut.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

public sealed record StoredDocument(Guid Id, string ObjectKey, string ChecksumSha256, long Size);

public sealed record ReconcileReport(IReadOnlyList<Guid> DbRowsMissingObject, IReadOnlyList<string> ObjectsMissingDbRow);

/// <summary>
/// Stores document bytes in MinIO (quarantine bucket, opaque key) and metadata in
/// PostgreSQL, computing the SHA-256 for integrity/provenance. Presigned URLs are
/// short-lived; the caller enforces permission first. Reconciliation finds DB↔object
/// drift so the two stores can be reconverified (KDM-025).
/// </summary>
public sealed class DocumentService(KorkutDbContext db, IObjectStoreService objectStoreService, IClock clock)
{
    public async Task<StoredDocument> StoreAsync(
        Guid officeId, Guid? ownerId, Stream content, string filename, string contentType, CancellationToken ct = default)
    {
        // Buffer + hash. Real streaming for large files is refined in KDM-026/027.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        var checksum = Convert.ToHexString(await SHA256.HashDataAsync(buffer, ct)).ToLowerInvariant();
        buffer.Position = 0;

        var key = ObjectBuckets.NewKey();
        await objectStoreService.PutAsync(ObjectBuckets.Quarantine, key, buffer, buffer.Length, contentType, ct);

        var doc = new DocumentEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            OwnerId = ownerId,
            Bucket = ObjectBuckets.Quarantine,
            ObjectKey = key,
            OriginalFilename = filename, // in the DB only, never in the key
            ContentType = contentType,
            Size = buffer.Length,
            ChecksumSha256 = checksum,
            Status = "quarantine",
            CreatedAt = clock.UtcNow,
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);
        return new StoredDocument(doc.Id, key, checksum, doc.Size);
    }

    /// <summary>Active (non-tombstoned) documents, newest first; optionally scoped to a matter.</summary>
    public Task<List<DocumentEntity>> ListAsync(Guid officeId, Guid? matterId = null, CancellationToken ct = default)
        => db.Documents.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.Status != "tombstoned" && (matterId == null || x.MatterId == matterId))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public Task<DocumentEntity?> GetAsync(Guid officeId, Guid documentId, CancellationToken ct = default)
        => db.Documents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == documentId && x.Status != "tombstoned", ct);

    /// <summary>Short-lived presigned GET. The caller MUST have checked permission first.</summary>
    public async Task<string> PresignAsync(Guid officeId, Guid documentId, TimeSpan expiry, CancellationToken ct = default)
    {
        var doc = await db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfficeId == officeId && x.Id == documentId, ct)
            ?? throw new InvalidOperationException("Document not found in this office.");
        // No download/preview/OCR until the document is proven clean (KDM-026).
        if (doc.Status != "clean")
        {
            throw new InvalidOperationException($"Document is not downloadable in state '{doc.Status}'.");
        }

        return await objectStoreService.PresignGetAsync(doc.Bucket, doc.ObjectKey, expiry, ct);
    }

    /// <summary>Find DB↔object drift for a bucket (orphan reconciliation).</summary>
    public async Task<ReconcileReport> ReconcileAsync(string bucket, CancellationToken ct = default)
    {
        var dbDocs = await db.Documents.AsNoTracking()
            .Where(x => x.Bucket == bucket)
            .Select(x => new { x.Id, x.ObjectKey })
            .ToListAsync(ct);
        var dbKeys = dbDocs.Select(d => d.ObjectKey).ToHashSet();

        var objectKeys = new HashSet<string>();
        await foreach (var k in objectStoreService.ListKeysAsync(bucket, ct))
        {
            objectKeys.Add(k);
        }

        var missingObject = dbDocs.Where(d => !objectKeys.Contains(d.ObjectKey)).Select(d => d.Id).ToList();
        var missingRow = objectKeys.Where(k => !dbKeys.Contains(k)).ToList();
        return new ReconcileReport(missingObject, missingRow);
    }
}
