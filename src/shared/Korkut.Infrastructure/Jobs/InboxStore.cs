using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;
using Korkut.Jobs.Entities;
using Korkut.Core;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Jobs;

/// <summary>
/// PostgreSQL idempotency ledger. The composite primary key (office, key) is what makes a
/// double insert impossible — the guarantee is the database's, not the caller's care.
/// </summary>
public sealed class InboxStore(KorkutDbContext db, IClock clock) : IInboxStore
{
    public Task<bool> IsProcessedAsync(Guid officeId, string idempotencyKey, CancellationToken cancellationToken)
        => db.Inbox.AnyAsync(x => x.OfficeId == officeId && x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task MarkProcessedAsync(Guid officeId, string idempotencyKey, CancellationToken cancellationToken)
    {
        db.Inbox.Add(new InboxEntry(officeId, idempotencyKey, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
    }
}
