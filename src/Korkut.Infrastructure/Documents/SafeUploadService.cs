using Korkut.Application.Auditing;
using Korkut.Application.Documents.Storage;
using Korkut.Application.Upload.Malware;
using Korkut.Application.Upload.SafeUpload;
using Korkut.Application.Upload.Validator;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Documents;

public sealed class SafeUploadService(
    KorkutDbContext db,
    DocumentService documents,
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
                await db.Documents.Where(x => x.Id == stored.Id).ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.Bucket, ObjectBuckets.Clean).SetProperty(x => x.Status, "clean"), ct);
                await audit.WriteAsync(new AuditEntry(officeId, ownerId, null, null, "upload.clean", "document", stored.Id, null, null), ct);
                return new SafeUploadResult(SafeUploadStatus.Clean, stored.Id, UploadValidatorRejection.None);

            case MalwareScanResult.Infected:
                await objectStoreService.RemoveAsync(ObjectBuckets.Quarantine, stored.ObjectKey, ct);
                await db.Documents.Where(x => x.Id == stored.Id).ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.Status, "rejected"), ct);
                await audit.WriteAsync(new AuditEntry(officeId, ownerId, null, null, "upload.malware", "document", stored.Id, null, null), ct);
                return new SafeUploadResult(SafeUploadStatus.Rejected, stored.Id, UploadValidatorRejection.Malware);

            default: // ScannerUnavailable — stays quarantine/pending; NOT clean
                await audit.WriteAsync(new AuditEntry(officeId, ownerId, null, null, "upload.scan_unavailable", "document", stored.Id, null, null), ct);
                return new SafeUploadResult(SafeUploadStatus.Pending, stored.Id, UploadValidatorRejection.ScannerUnavailable);
        }
    }
}
