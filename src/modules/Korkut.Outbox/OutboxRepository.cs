using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Outbox;

/// <summary>EF/Postgres adapter for <see cref="IOutboxRepository"/> over the ambient DbContext.</summary>
public sealed class OutboxRepository(DbContext db) : Repository<OutboundMessage>(db), IOutboxRepository
{
    public override async Task<IReadOnlyList<OutboundMessage>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
}
