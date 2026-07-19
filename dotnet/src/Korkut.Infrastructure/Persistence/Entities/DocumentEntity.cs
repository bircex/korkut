namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// Document metadata (bytes live in MinIO). The object key is opaque; the original
/// filename lives ONLY here in the DB, never in the key. Status tracks the
/// quarantine→clean lifecycle (KDM-025/026).
/// </summary>
public sealed class DocumentEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid? MatterId { get; set; }

    public Guid? ClientId { get; set; }

    public Guid? OwnerId { get; set; }

    public string Bucket { get; set; } = string.Empty;

    /// <summary>Opaque object key (no filename/PII).</summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>Original filename — stored here, never in the object key.</summary>
    public string OriginalFilename { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long Size { get; set; }

    /// <summary>Lower-case hex SHA-256 of the bytes (integrity + dedup + provenance).</summary>
    public string ChecksumSha256 { get; set; } = string.Empty;

    /// <summary>"quarantine" | "clean" | "rejected".</summary>
    public string Status { get; set; } = "quarantine";

    public int Version { get; set; } = 1;

    public DateTimeOffset? RetentionUntil { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
