using Korkut.Core;
using Korkut.Application.Documents.Storage;
using Korkut.Application.Upload.Malware;
using Korkut.Application.Upload.SafeUpload;
using Korkut.Application.Upload.Validator;
using Korkut.Documents;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Documents;

public sealed class SafeUploadService(
    KorkutDbContext db,
    DocumentStorageService documents,
    IObjectStoreService objectStoreService,
    IUploadValidatorService validatorService,
    IMalwareScanService scannerService,
    IAuditLog audit): ISafeUploadService
{
    public async Task<SafeUploadResult> IngestAsync(
        Guid officeId, Guid? ownerId, Stream content, string declaredFilename, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var check = validatorService.Validate(declaredFilename, buffer);
        
        if (!check.Ok)
        {
            await audit.WriteAsync(new AuditEntry(officeId, ownerId, null, null, "upload.rejected", "document", null,
                new { reason = check.ValidatorRejection.ToString(), filename = check.SafeFilename }, null), ct);
            return new SafeUploadResult(SafeUploadStatus.Rejected, null, check.ValidatorRejection);
        }

        buffer.Position = 0;
        var stored = await documents.StoreAsync(
            officeId, ownerId, buffer, check.SafeFilename!, $"application/{check.DetectedType}", ct);

        MalwareScanResult scan;
        
        try
        {
            buffer.Position = 0;
            scan = await scannerService.ScanAsync(buffer, ct);
        }
        catch (Exception)
        {
            scan = MalwareScanResult.ScannerUnavailable; // fail-closed
        }

        switch (scan)
        {
            case MalwareScanResult.Clean:
                await objectStoreService.CopyAsync(ObjectBuckets.Quarantine, ObjectBuckets.Clean, stored.ObjectKey, ct);
                await objectStoreService.RemoveAsync(ObjectBuckets.Quarantine, stored.ObjectKey, ct);
                await MarkAsync(stored.Id, doc => doc.MarkClean(ObjectBuckets.Clean), ct);
                await audit.WriteAsync(new AuditEntry(officeId, ownerId, null, null, "upload.clean", "document", stored.Id, null, null), ct);
                return new SafeUploadResult(SafeUploadStatus.Clean, stored.Id, UploadValidatorRejection.None);

            case MalwareScanResult.Infected:
                await objectStoreService.RemoveAsync(ObjectBuckets.Quarantine, stored.ObjectKey, ct);
                await MarkAsync(stored.Id, doc => doc.MarkRejected(), ct);
                await audit.WriteAsync(new AuditEntry(officeId, ownerId, null, null, "upload.malware", "document", stored.Id, null, null), ct);
                return new SafeUploadResult(SafeUploadStatus.Rejected, stored.Id, UploadValidatorRejection.Malware);

            default: // ScannerUnavailable — stays quarantine/pending; NOT clean
                await audit.WriteAsync(new AuditEntry(officeId, ownerId, null, null, "upload.scan_unavailable", "document", stored.Id, null, null), ct);
                return new SafeUploadResult(SafeUploadStatus.Pending, stored.Id, UploadValidatorRejection.ScannerUnavailable);
        }
    }

    /// <summary>The scan verdict is a state change on the document, so it goes through the aggregate.</summary>
    private async Task MarkAsync(Guid documentId, Action<Document> verdict, CancellationToken ct)
    {
        var document = await db.Documents.SingleAsync(x => x.Id == documentId, ct);
        verdict(document);
        await db.SaveChangesAsync(ct);
    }
}
