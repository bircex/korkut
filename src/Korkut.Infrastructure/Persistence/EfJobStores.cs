using Korkut.Application.Jobs;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL-backed outbox. Leasing uses <c>FOR UPDATE SKIP LOCKED</c> so multiple
/// workers can drain the queue concurrently without double-processing — the
/// production form of the in-memory reference store from KDM-008.
/// </summary>
public sealed class EfOutboxStore(KorkutDbContext db, IClock clock) : IOutboxStore
{
    public async Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        db.Outbox.Add(new OutboxEntity
        {
            Id = message.Id,
            OfficeId = message.OfficeId,
            Type = message.Type,
            Payload = message.Payload,
            IdempotencyKey = message.IdempotencyKey,
            Status = JobStatus.Pending,
            Attempts = message.Attempts,
            AvailableAt = message.AvailableAt == default ? now : message.AvailableAt,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> LeaseBatchAsync(
        string owner, TimeSpan leaseDuration, DateTimeOffset now, int max, CancellationToken cancellationToken)
    {
        var leaseUntil = now + leaseDuration;
        // Atomic lease: pick due/expired rows, lock them skipping already-locked ones,
        // flip to Leased and return them. One round-trip, safe under concurrency.
        var rows = await db.Outbox.FromSqlInterpolated($@"
            WITH due AS (
                SELECT id FROM korkut.outbox_messages
                WHERE ((status IN ('Pending','Failed') AND available_at <= {now})
                    OR (status = 'Leased' AND lease_until <= {now}))
                ORDER BY available_at
                LIMIT {max}
                FOR UPDATE SKIP LOCKED
            )
            UPDATE korkut.outbox_messages m
            SET status = 'Leased', lease_owner = {owner}, lease_until = {leaseUntil}, updated_at = {now}
            FROM due WHERE m.id = due.id
            RETURNING m.*")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public Task MarkCompletedAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken) =>
        UpdateAsync(id, e =>
        {
            e.Status = JobStatus.Completed;
            e.LeaseOwner = null;
            e.LeaseUntil = null;
            e.UpdatedAt = now;
        }, cancellationToken);

    public Task MarkFailedAsync(
        Guid id, int attempts, DateTimeOffset availableAt, string error, CancellationToken cancellationToken) =>
        UpdateAsync(id, e =>
        {
            e.Status = JobStatus.Failed;
            e.Attempts = attempts;
            e.AvailableAt = availableAt;
            e.LastError = error;
            e.LeaseOwner = null;
            e.LeaseUntil = null;
            e.UpdatedAt = availableAt;
        }, cancellationToken);

    public Task DeadLetterAsync(Guid id, string error, DateTimeOffset now, CancellationToken cancellationToken) =>
        UpdateAsync(id, e =>
        {
            e.Status = JobStatus.DeadLettered;
            e.LastError = error;
            e.LeaseOwner = null;
            e.LeaseUntil = null;
            e.UpdatedAt = now;
        }, cancellationToken);

    public async Task<OutboxMessage?> ReplayAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var e = await db.Outbox.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (e is null || e.Status != JobStatus.DeadLettered)
        {
            return null;
        }

        e.Status = JobStatus.Pending;
        e.Attempts = 0;
        e.AvailableAt = now;
        e.LastError = null;
        e.LeaseOwner = null;
        e.LeaseUntil = null;
        e.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return Map(e);
    }

    private async Task UpdateAsync(Guid id, Action<OutboxEntity> mutate, CancellationToken cancellationToken)
    {
        var e = await db.Outbox.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (e is null)
        {
            return;
        }

        mutate(e);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static OutboxMessage Map(OutboxEntity e) => new()
    {
        Id = e.Id,
        OfficeId = e.OfficeId,
        Type = e.Type,
        Payload = e.Payload,
        IdempotencyKey = e.IdempotencyKey,
        Status = e.Status,
        Attempts = e.Attempts,
        AvailableAt = e.AvailableAt,
        LeaseOwner = e.LeaseOwner,
        LeaseUntil = e.LeaseUntil,
        LastError = e.LastError,
    };
}

/// <summary>PostgreSQL idempotency ledger. Composite PK (office, key) makes double-insert impossible.</summary>
public sealed class EfInboxStore(KorkutDbContext db, IClock clock) : IInboxStore
{
    public Task<bool> IsProcessedAsync(Guid officeId, string idempotencyKey, CancellationToken cancellationToken) =>
        db.Inbox.AnyAsync(x => x.OfficeId == officeId && x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task MarkProcessedAsync(Guid officeId, string idempotencyKey, CancellationToken cancellationToken)
    {
        db.Inbox.Add(new InboxEntity
        {
            OfficeId = officeId,
            IdempotencyKey = idempotencyKey,
            ProcessedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>PostgreSQL append-only job audit.</summary>
public sealed class EfJobAudit(KorkutDbContext db) : IJobAudit
{
    public async Task RecordAsync(JobAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        db.JobAudit.Add(new JobAuditEntity
        {
            Id = Korkut.SharedKernel.Ids.UuidV7.NewId(),
            MessageId = auditEvent.MessageId,
            OfficeId = auditEvent.OfficeId,
            Type = auditEvent.Type,
            Kind = auditEvent.Kind,
            Detail = auditEvent.Detail,
            At = auditEvent.At,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
