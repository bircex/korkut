namespace Korkut.Domain.Time;

/// <summary>
/// Abstraction over "now" so domain/application code never reads the ambient
/// system clock directly (keeps them deterministic and testable).
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Production clock backed by the system UTC time.</summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
