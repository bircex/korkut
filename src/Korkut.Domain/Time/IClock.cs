namespace Korkut.Domain.Time;

/// <summary>
/// Abstraction over "now" so domain/application code never reads the ambient
/// system clock directly (keeps them deterministic and testable).
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
