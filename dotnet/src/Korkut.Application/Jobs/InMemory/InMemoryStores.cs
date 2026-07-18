using System.Collections.Concurrent;

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
            _messages[message.Id] = message with { Status = JobStatus.Pending };
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxMessage>> LeaseBatchAsync(
        string owner, TimeSpan leaseDuration, DateTimeOffset now, int max, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var due = _messages.Values
                .Where(m => IsLeasable(m, now))
                .OrderBy(m => m.AvailableAt)
                .Take(max)
                .ToList();

            var leased = new List<OutboxMessage>(due.Count);
            foreach (var m in due)
            {
                var updated = m with
                {
                    Status = JobStatus.Leased,
                    LeaseOwner = owner,
                    LeaseUntil = now + leaseDuration,
                };
                _messages[m.Id] = updated;
                leased.Add(updated);
            }

            return Task.FromResult<IReadOnlyList<OutboxMessage>>(leased);
        }
    }

    // Leasable = pending/failed and due, OR a leased message whose lease has expired (crashed worker).
    private static bool IsLeasable(OutboxMessage m, DateTimeOffset now) =>
        (m.Status is JobStatus.Pending or JobStatus.Failed && m.AvailableAt <= now)
        || (m.Status is JobStatus.Leased && m.LeaseUntil is { } until && until <= now);

    public Task MarkCompletedAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Mutate(id, m => m with { Status = JobStatus.Completed, LeaseOwner = null, LeaseUntil = null });
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(
        Guid id, int attempts, DateTimeOffset availableAt, string error, CancellationToken cancellationToken)
    {
        Mutate(id, m => m with
        {
            Status = JobStatus.Failed,
            Attempts = attempts,
            AvailableAt = availableAt,
            LastError = error,
            LeaseOwner = null,
            LeaseUntil = null,
        });
        return Task.CompletedTask;
    }

    public Task DeadLetterAsync(Guid id, string error, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Mutate(id, m => m with
        {
            Status = JobStatus.DeadLettered,
            LastError = error,
            LeaseOwner = null,
            LeaseUntil = null,
        });
        return Task.CompletedTask;
    }

    public Task<OutboxMessage?> ReplayAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_messages.TryGetValue(id, out var m) || m.Status != JobStatus.DeadLettered)
            {
                return Task.FromResult<OutboxMessage?>(null);
            }

            var replayed = m with
            {
                Status = JobStatus.Pending,
                Attempts = 0,
                AvailableAt = now,
                LastError = null,
                LeaseOwner = null,
                LeaseUntil = null,
            };
            _messages[id] = replayed;
            return Task.FromResult<OutboxMessage?>(replayed);
        }
    }

    private void Mutate(Guid id, Func<OutboxMessage, OutboxMessage> update)
    {
        lock (_gate)
        {
            if (_messages.TryGetValue(id, out var m))
            {
                _messages[id] = update(m);
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
