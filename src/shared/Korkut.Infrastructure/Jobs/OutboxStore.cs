using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;
using Korkut.Jobs.Entities;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Jobs;

/// <summary>
/// PostgreSQL-backed outbox. Claiming a batch uses <c>FOR UPDATE SKIP LOCKED</c>, so many
/// workers can drain the queue at once without two of them taking the same message. Every
/// other transition goes through the aggregate, so the rules are the same ones the
/// in-memory reference store applies.
/// </summary>
public sealed class OutboxStore(KorkutDbContext db) : IOutboxStore
{
    public async Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        db.Outbox.Add(message);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> LeaseBatchAsync(
        string owner, TimeSpan leaseDuration, DateTimeOffset now, int max, CancellationToken cancellationToken)
    {
        var leaseUntil = now + leaseDuration;
        // One atomic statement: pick the due rows, lock them skipping the ones another
        // worker already holds, flip them to Leased and return them. The WHERE clause is
        // the SQL form of OutboxMessage.IsLeasable — the one rule that cannot live on the
        // aggregate, because claiming has to be a single round-trip to stay race-free.
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

        return rows;
    }

    public Task MarkCompletedAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
        => MutateAsync(id, m => m.Complete(now), cancellationToken);

    public Task MarkFailedAsync(
        Guid id, int attempts, DateTimeOffset availableAt, string error, CancellationToken cancellationToken)
        => MutateAsync(id, m => m.Fail(attempts, availableAt, error), cancellationToken);

    public Task DeadLetterAsync(Guid id, string error, DateTimeOffset now, CancellationToken cancellationToken)
        => MutateAsync(id, m => m.DeadLetter(error, now), cancellationToken);

    public async Task<OutboxMessage?> ReplayAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var message = await db.Outbox.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message is null || !message.Replay(now))
        {
            return null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return message;
    }

    private async Task MutateAsync(Guid id, Action<OutboxMessage> transition, CancellationToken cancellationToken)
    {
        var message = await db.Outbox.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message is null)
        {
            return;
        }

        transition(message);
        await db.SaveChangesAsync(cancellationToken);
    }
}
