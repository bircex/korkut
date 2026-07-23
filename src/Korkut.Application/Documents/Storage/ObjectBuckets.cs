using Korkut.Domain.Common;

namespace Korkut.Application.Documents.Storage;

/// <summary>Bucket names + opaque object-key generation (no PII).</summary>
public static class ObjectBuckets
{
    public const string Quarantine = "korkut-quarantine";
    
    public const string Clean = "korkut-clean";

    /// <summary>An opaque key: a UUIDv7, sharded by its first byte. No filename/PII.</summary>
    public static string NewKey()
    {
        var id = UuidV7.NewId().ToString("N");
        return $"{id[..2]}/{id}";
    }
}
