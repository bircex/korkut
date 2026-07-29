using Korkut.Jobs.Entities;
using Korkut.Jobs.Abstractions;
namespace Korkut.Jobs.Services;

/// <summary>
/// AsyncLocal tenant scope. The dispatcher enters the message's office before
/// invoking a handler, so handlers (and the persistence layer wired in KDM-012)
/// read the tenant from the trusted ambient context — never from the payload.
/// </summary>
public sealed class AmbientTenantContext : ITenantContext
{
    private static readonly AsyncLocal<Guid?> Current = new();

    public Guid? CurrentOfficeId => Current.Value;

    public IDisposable Enter(Guid officeId)
    {
        var previous = Current.Value;
        Current.Value = officeId;
        return new Scope(() => Current.Value = previous);
    }

    private sealed class Scope(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}

/// <summary>Exponential backoff (2^attempt seconds) capped, with a dead-letter threshold.</summary>
public sealed class ExponentialRetryPolicy(int maxAttempts = 5, double baseSeconds = 2, double maxSeconds = 300)
    : IRetryPolicy
{
    public int MaxAttempts { get; } = maxAttempts;

    public TimeSpan BackoffFor(int attempt)
    {
        var seconds = Math.Min(maxSeconds, baseSeconds * Math.Pow(2, Math.Max(0, attempt - 1)));
        return TimeSpan.FromSeconds(seconds);
    }
}
