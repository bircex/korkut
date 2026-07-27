using Korkut.Channels.Entities;
using Korkut.Channels;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Channels;

/// <summary>
/// EF/Postgres adapter for <see cref="IChannelRepository"/> over the ambient DbContext. The
/// aggregate is keyed by (office, kind), so it cannot derive from the generic EF base; every
/// query still pins the office before Postgres RLS is consulted.
/// </summary>
internal sealed class ChannelRepository(DbContext db) : IChannelRepository
{
    public Task<Channel?> FindAsync(Guid officeId, string kind, CancellationToken ct = default)
        => Scoped(officeId).AsNoTracking().SingleOrDefaultAsync(c => c.Kind == kind, ct);

    public Task<Channel?> FindForUpdateAsync(Guid officeId, string kind, CancellationToken ct = default)
        => Scoped(officeId).SingleOrDefaultAsync(c => c.Kind == kind, ct);

    public async Task<IReadOnlyList<Channel>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().ToListAsync(ct);

    public Task AddAsync(Channel channel, CancellationToken ct = default)
    {
        db.Set<Channel>().Add(channel);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    private IQueryable<Channel> Scoped(Guid officeId) => db.Set<Channel>().Where(c => c.OfficeId == officeId);
}
