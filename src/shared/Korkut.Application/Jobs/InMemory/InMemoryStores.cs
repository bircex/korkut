using System.Collections.Concurrent;
using Korkut.Domain.Jobs;

namespace Korkut.Application.Jobs.InMemory;

/// <summary>
/// In-memory reference outbox for tests and single-node/dev use. Leasing,
/// completion, failure, dead-letter and replay are serialized under a lock so the
/// semantics match what the EF-backed store (KDM-011) will enforce with row locks
/// / SELECT ... FOR UPDATE SKIP LOCKED.
/// </summary>
public sealed class InMemoryOutboxStore : IOutboxStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, OutboxMessage> _messages = [];

    public IReadOnlyCollection<OutboxMessage> Snapshot()
    {
        lock (_gate)
        {
            return _messages.Values.ToList();
        }
    }

    public Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _messages[message.Id] = message;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxMessage>> LeaseBatchAsync(
        string owner, TimeSpan leaseDuration, DateTimeOffset now, int max, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var due = _messages.Values
                .Where(m => m.IsLeasable(now))
                .OrderBy(m => m.AvailableAt)
                .Take(max)
                .ToList();

            foreach (var message in due)
            {
                message.Lease(owner, now + leaseDuration, now);
            }

            return Task.FromResult<IReadOnlyList<OutboxMessage>>(due);
        }
    }

    public Task MarkCompletedAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Mutate(id, m => m.Complete(now));
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(
        Guid id, int attempts, DateTimeOffset availableAt, string error, CancellationToken cancellationToken)
    {
        Mutate(id, m => m.Fail(attempts, availableAt, error));
        return Task.CompletedTask;
    }

    public Task DeadLetterAsync(Guid id, string error, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Mutate(id, m => m.DeadLetter(error, now));
        return Task.CompletedTask;
    }

    public Task<OutboxMessage?> ReplayAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(
                _messages.TryGetValue(id, out var message) && message.Replay(now) ? message : null);
        }
    }

    private void Mutate(Guid id, Action<OutboxMessage> update)
    {
        lock (_gate)
        {
            if (_messages.TryGetValue(id, out var message))
            {
                update(message);
            }
        }
    }
}

/// <summary>In-memory idempotency ledger keyed by (office, key).</summary>
public sealed class InMemoryInboxStore : IInboxStore
{
    private readonly ConcurrentDictionary<(Guid, string), byte> _processed = new();

    public Task<bool> IsProcessedAsync(Guid officeId, string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(_processed.ContainsKey((officeId, idempotencyKey)));

    public Task MarkProcessedAsync(Guid officeId, string idempotencyKey, CancellationToken cancellationToken)
    {
        _processed[(officeId, idempotencyKey)] = 1;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory append-only job audit.</summary>
public sealed class InMemoryJobAudit : IJobAudit
{
    private readonly ConcurrentQueue<JobAuditEvent> _events = new();

    public IReadOnlyList<JobAuditEvent> Events => _events.ToList();

    public Task RecordAsync(JobAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        _events.Enqueue(auditEvent);
        return Task.CompletedTask;
    }
}
