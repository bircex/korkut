namespace Korkut.Domain.Common;

/// <summary>
/// Single UUIDv7 identity strategy for the whole platform (per the architecture
/// decision: one id strategy, no bigint or dual public/internal ids). UUIDv7 is
/// time-ordered, so it indexes well as a primary key while staying non-guessable.
/// </summary>
public static class UuidV7
{
    /// <summary>Create a new time-ordered UUIDv7.</summary>
    public static Guid NewId() => Guid.CreateVersion7();

    /// <summary>Create a UUIDv7 for a specific timestamp (useful in tests/backfills).</summary>
    public static Guid NewId(DateTimeOffset timestamp) => Guid.CreateVersion7(timestamp);
}
