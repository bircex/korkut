using Korkut.Domain.Jobs;
using Korkut.Application.Jobs;
using Korkut.Core;

namespace Korkut.Application.Tests;

/// <summary>Mutable clock so tests can advance time to exercise backoff/lease expiry.</summary>
public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>
/// Configurable test handler: counts invocations, records the ambient tenant seen
/// at handle time, and can be told to throw on the first N calls (to simulate
/// transient failures / timeouts / poison messages).
/// </summary>
public sealed class RecordingHandler(string messageType, ITenantContext tenant, int throwFirst = 0) : IJobHandler
{
    private int _remainingThrows = throwFirst;

    public string MessageType { get; } = messageType;

    public int Invocations { get; private set; }

    public List<Guid?> SeenTenants { get; } = [];

    public bool AlwaysThrow { get; init; }

    public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        Invocations++;
        SeenTenants.Add(tenant.CurrentOfficeId);

        if (AlwaysThrow || _remainingThrows > 0)
        {
            _remainingThrows--;
            throw new InvalidOperationException($"boom for {message.Id}");
        }

        return Task.CompletedTask;
    }
}
