namespace Korkut.Application.Documents;

/// <summary>
/// Object-byte storage (MinIO). Buckets are private; keys are opaque and contain no
/// filename, tenant/client name or legal PII. Uploads land in the quarantine bucket
/// and are only promoted to clean after validation + scanning (KDM-026/027).
/// </summary>
public interface IObjectStore
{
    Task EnsureBucketsAsync(CancellationToken ct = default);

    Task PutAsync(string bucket, string key, Stream data, long size, string contentType, CancellationToken ct = default);

    /// <summary>Copies the object bytes into a seekable stream (caller disposes).</summary>
    Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default);

    Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct = default);

    Task CopyAsync(string fromBucket, string toBucket, string key, CancellationToken ct = default);

    Task RemoveAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>A short-lived, permission-checked (by the caller) presigned GET URL.</summary>
    Task<string> PresignGetAsync(string bucket, string key, TimeSpan expiry, CancellationToken ct = default);

    /// <summary>All keys in a bucket — used for orphan reconciliation.</summary>
    IAsyncEnumerable<string> ListKeysAsync(string bucket, CancellationToken ct = default);
}

/// <summary>Bucket names + opaque object-key generation (no PII).</summary>
public static class ObjectBuckets
{
    public const string Quarantine = "korkut-quarantine";
    public const string Clean = "korkut-clean";

    /// <summary>An opaque key: a UUIDv7, sharded by its first byte. No filename/PII.</summary>
    public static string NewKey()
    {
        var id = Korkut.SharedKernel.Ids.UuidV7.NewId().ToString("N");
        return $"{id[..2]}/{id}";
    }
}
