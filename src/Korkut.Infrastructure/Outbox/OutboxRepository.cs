using Korkut.Application.Outbox;
using Korkut.Domain.Outbox;
using Korkut.Persistence;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Outbox;

/// <summary>EF/Postgres adapter for <see cref="IOutboxRepository"/>.</summary>
public sealed class OutboxRepository(KorkutDbContext db) : Repository<OutboundMessage>(db), IOutboxRepository
{
    public override async Task<IReadOnlyList<OutboundMessage>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
}
