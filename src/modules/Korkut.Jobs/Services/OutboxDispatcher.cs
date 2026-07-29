using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Entities;
using Korkut.Core;

namespace Korkut.Jobs.Services;

/// <summary>
/// Drives one pass of the outbox: leases a batch, and for each message establishes
/// the tenant scope, enforces idempotency via the inbox, invokes the handler, and
/// applies retry/backoff or dead-letters a poison message. A failure is isolated
/// to its own message — it never blocks the rest of the batch or other tenants.
/// </summary>
public sealed class OutboxDispatcher(
    IOutboxStore outbox,
    IInboxStore inbox,
    IJobAudit audit,
    IReadOnlyDictionary<string, IJobHandler> handlers,
    ITenantContext tenant,
    IRetryPolicy retry,
    IClock clock)
{
    /// <summary>Process up to <paramref name="batchSize"/> due messages; returns the count handled.</summary>
    public async Task<int> DispatchOnceAsync(
        string owner, int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var leased = await outbox.LeaseBatchAsync(owner, leaseDuration, now, batchSize, cancellationToken);
        var handled = 0;

        foreach (var message in leased)
        {
            try
            {
                await ProcessAsync(message, cancellationToken);
                handled++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Isolate the failure to this message; the loop continues with the rest.
                await HandleFailureAsync(message, ex, cancellationToken);
            }
        }

        return handled;
    }

    private async Task ProcessAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var completedAt = clock.UtcNow;

        // Tenant is taken from the trusted message metadata, never from payload contents.
        using (tenant.Enter(message.OfficeId))
        {
            if (await inbox.IsProcessedAsync(message.OfficeId, message.IdempotencyKey, cancellationToken))
            {
                // A redelivery / duplicate: the logical effect already happened — complete without re-running.
                await outbox.MarkCompletedAsync(message.Id, completedAt, cancellationToken);
                await Audit(message, JobAuditKind.DuplicateSkipped, null, completedAt, cancellationToken);
                return;
            }

            if (!handlers.TryGetValue(message.Type, out var handler))
            {
                throw new InvalidOperationException($"No handler registered for job type '{message.Type}'.");
            }

            await handler.HandleAsync(message, cancellationToken);
            await inbox.MarkProcessedAsync(message.OfficeId, message.IdempotencyKey, cancellationToken);
            await outbox.MarkCompletedAsync(message.Id, completedAt, cancellationToken);
            await Audit(message, JobAuditKind.Completed, null, completedAt, cancellationToken);
        }
    }

    private async Task HandleFailureAsync(OutboxMessage message, Exception ex, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var attempts = message.Attempts + 1;

        if (attempts >= retry.MaxAttempts)
        {
            await outbox.DeadLetterAsync(message.Id, ex.Message, now, cancellationToken);
            await Audit(message, JobAuditKind.DeadLettered, ex.Message, now, cancellationToken);
            return;
        }

        var availableAt = now + retry.BackoffFor(attempts);
        await outbox.MarkFailedAsync(message.Id, attempts, availableAt, ex.Message, cancellationToken);
        await Audit(message, JobAuditKind.Retried, ex.Message, now, cancellationToken);
    }

    private Task Audit(OutboxMessage m, JobAuditKind kind, string? detail, DateTimeOffset at, CancellationToken ct) =>
        audit.RecordAsync(new JobAuditEvent(m.Id, m.OfficeId, m.Type, kind, detail, at), ct);
}
