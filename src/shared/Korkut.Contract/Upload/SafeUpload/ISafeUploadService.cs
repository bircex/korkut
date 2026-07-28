namespace Korkut.Contract.Upload.SafeUpload;

/// <summary>
/// The layered ingest pipeline (KDM-026): validate → store in QUARANTINE → scan
/// (fail-closed) → promote to CLEAN or reject. Nothing is downloadable/previewable/
/// A missing/erroring scanner leaves the document PENDING, never clean.
/// </summary>
public interface ISafeUploadService
{
    Task<SafeUploadResult> IngestAsync(
        Guid officeId, Guid? ownerId, Stream content, string declaredFilename, CancellationToken ct = default);
}
