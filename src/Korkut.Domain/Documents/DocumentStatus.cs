namespace Korkut.Domain.Documents;

/// <summary>The quarantine → clean lifecycle a document's bytes go through.</summary>
public static class DocumentStatus
{
    /// <summary>Stored, but not yet proven safe: nothing may read it.</summary>
    public const string Quarantine = "quarantine";

    /// <summary>Scanned and safe: the only state a document can be downloaded in.</summary>
    public const string Clean = "clean";

    /// <summary>Refused by the scanner; the bytes are gone, the row is kept as a record.</summary>
    public const string Rejected = "rejected";

    /// <summary>Deleted: bytes and extraction artifacts purged, metadata retained as a marker.</summary>
    public const string Tombstoned = "tombstoned";
}
