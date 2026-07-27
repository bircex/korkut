namespace Korkut.Core;

public static class UuidV7
{
    /// <summary>Create a new time-ordered UUIDv7.</summary>
    public static Guid NewId() => Guid.CreateVersion7();

    /// <summary>Create a UUIDv7 for a specific timestamp (useful in tests/backfills).</summary>
    public static Guid NewId(DateTimeOffset timestamp) => Guid.CreateVersion7(timestamp);
}
