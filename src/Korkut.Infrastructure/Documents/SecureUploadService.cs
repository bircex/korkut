using Korkut.Application.Auditing;
using Korkut.Application.Documents;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Documents;

public enum UploadStatus
{
    Clean,
    Rejected,
    Pending, // scanner unavailable — fail-closed, awaiting a successful scan
}

public sealed record UploadOutcome(UploadStatus Status, Guid? DocumentId, UploadRejection Rejection);

/// <summary>
/// The layered ingest pipeline (KDM-026): validate → store in QUARANTINE → scan
/// (fail-closed) → promote to CLEAN or reject. Nothing is downloadable/previewable/
/// OCR-able until it reaches "clean" (enforced by <see cref="DocumentService.PresignAsync"/>).
/// A missing/erroring scanner leaves the document PENDING, never clean.
/// </summary>
public sealed class SecureUploadService(
    KorkutDbContext db,
    DocumentService documents,
    IObjectStore objectStore,
    UploadValidator validator,
    IMalwareScanner scanner,
    IAuditLog audit)
{
    public async Task<UploadOutcome> IngestAsync(
        Guid officeId, Guid? ownerId, Stream content, string declaredFilename, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var check = validator.Validate(declaredFilename, buffer);
        if (!check.Ok)
        {
            await audit.WriteAsync(new AuditEntry(officeId, ownerId, null, null, "upload.rejected", "document", null,
                new { reason = check.Rejection.ToString(), filename = check.SafeFilename }, null), ct);
            return new UploadOutcome(UploadStatus.Rejected, null, check.Rejection);
        }

        buffer.Position = 0;
        var stored = await documents.StoreAsync(
            officeId, ownerId, buffer, check.SafeFilename!, $"application/{check.DetectedType}", ct);

        ScanResult scan;
        try
        {
            buffer.Position = 0;
            scan = await scanner.ScanAsync(buffer, ct);
        }
        catch (Exception)
        {
            scan = ScanResult.ScannerUnavailable; // fail-closed
        }

        switch (scan)
        {
            case ScanResult.Clean:
                await objectStore.CopyAsync(ObjectBuckets.Quarantine, ObjectBuckets.Clean, stored.ObjectKey, ct);
                await objectStore.RemoveAsync(ObjectBuckets.Quarantine, stored.ObjectKey, ct);
                await db.Documents.Where(x => x.Id == stored.Id).ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.Bucket, ObjectBuckets.Clean).SetProperty(x => x.Status, "clean"), ct);
                await audit.WriteAsync(new AuditEntry(officeId, ownerId, null, null, "upload.clean", "document", stored.Id, null, null), ct);
                return new UploadOutcome(UploadStatus.Clean, stored.Id, UploadRejection.None);

            case ScanResult.Infected:
                await objectStore.RemoveAsync(ObjectBuckets.Quarantine, stored.ObjectKey, ct);
                await db.Documents.Where(x => x.Id == stored.Id).ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.Status, "rejected"), ct);
                await audit.WriteAsync(new AuditEntry(officeId, ownerId, null, null, "upload.malware", "document", stored.Id, null, null), ct);
                return new UploadOutcome(UploadStatus.Rejected, stored.Id, UploadRejection.Malware);

            default: // ScannerUnavailable — stays quarantine/pending; NOT clean
                await audit.WriteAsync(new AuditEntry(officeId, ownerId, null, null, "upload.scan_unavailable", "document", stored.Id, null, null), ct);
                return new UploadOutcome(UploadStatus.Pending, stored.Id, UploadRejection.ScannerUnavailable);
        }
    }
}
