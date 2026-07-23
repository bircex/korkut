using Korkut.Domain.Common;

namespace Korkut.Domain.Documents;

/// <summary>
/// A document's metadata; the bytes live in object storage. Two rules make this an
/// aggregate rather than a row: nothing may be read until the scan has proven it clean,
/// and a document under legal hold cannot be deleted by anyone or expired by retention.
/// The original filename is held here and never in the object key, so the key leaks
/// nothing about its contents.
/// </summary>
public sealed class Document : Entity, ITenantOwned, IAggregateRoot
{
    public Document(
        Guid officeId, Guid? ownerId, string bucket, string objectKey, string originalFilename,
        string contentType, long size, string checksumSha256, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        OwnerId = ownerId;
        Bucket = bucket;
        ObjectKey = objectKey;
        OriginalFilename = originalFilename;
        ContentType = contentType;
        Size = size;
        ChecksumSha256 = checksumSha256;
        Status = DocumentStatus.Quarantine;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Document()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid? MatterId { get; private set; }

    public Guid? ClientId { get; private set; }

    public Guid? OwnerId { get; private set; }

    public string Bucket { get; private set; } = string.Empty;

    /// <summary>Opaque object key (no filename, no PII).</summary>
    public string ObjectKey { get; private set; } = string.Empty;

    /// <summary>Original filename — stored here, never in the object key.</summary>
    public string OriginalFilename { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = "application/octet-stream";

    public long Size { get; private set; }

    /// <summary>Lower-case hex SHA-256 of the bytes (integrity, dedup, provenance).</summary>
    public string ChecksumSha256 { get; private set; } = string.Empty;

    /// <summary>"quarantine" | "clean" | "rejected" | "tombstoned".</summary>
    public string Status { get; private set; } = DocumentStatus.Quarantine;

    public int Version { get; private set; } = 1;

    public DateTimeOffset? RetentionUntil { get; private set; }

    /// <summary>
    /// Under legal hold: exempt from deletion and from retention purges, whatever
    /// <see cref="RetentionUntil"/> says.
    /// </summary>
    public bool LegalHold { get; private set; }

    /// <summary>Set when the document is tombstoned (bytes purged, metadata retained).</summary>
    public DateTimeOffset? TombstonedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsTombstoned => Status == DocumentStatus.Tombstoned;

    /// <summary>Only a scanned-clean document may be read; everything else is unreadable by design.</summary>
    public bool IsDownloadable => Status == DocumentStatus.Clean;

    /// <summary>The scan passed: the bytes moved to the clean bucket and may now be read.</summary>
    public void MarkClean(string cleanBucket)
    {
        Bucket = cleanBucket;
        Status = DocumentStatus.Clean;
    }

    /// <summary>The scan found malware; the bytes are discarded and the row records the refusal.</summary>
    public void MarkRejected() => Status = DocumentStatus.Rejected;

    /// <summary>
    /// Marks the document deleted after its artifacts were purged. False when it is already
    /// tombstoned, so the caller can treat a repeat delete as the no-op it is.
    /// </summary>
    public bool Tombstone(DateTimeOffset tombstonedAt)
    {
        if (IsTombstoned)
        {
            return false;
        }

        Status = DocumentStatus.Tombstoned;
        TombstonedAt = tombstonedAt;
        return true;
    }

    /// <summary>Places or lifts the legal hold that vetoes deletion and retention.</summary>
    public void SetLegalHold(bool held) => LegalHold = held;

    /// <summary>Retention has elapsed and nothing is holding the document back.</summary>
    public bool IsExpired(DateTimeOffset now)
        => RetentionUntil is { } until && until < now && !LegalHold && !IsTombstoned;
}
