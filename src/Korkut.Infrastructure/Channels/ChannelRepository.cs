using Korkut.Application.Channels;
using Korkut.Domain.Channels;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Channels;

/// <summary>
/// EF/Postgres adapter for <see cref="IChannelRepository"/>. The aggregate is keyed by
/// (office, kind), so this one cannot derive from the generic EF base; every query still
/// pins the office before Postgres RLS is consulted.
/// </summary>
public sealed class ChannelRepository(KorkutDbContext db) : IChannelRepository
{
    public Task<Channel?> FindAsync(Guid officeId, string kind, CancellationToken ct = default)
        => Scoped(officeId).AsNoTracking().SingleOrDefaultAsync(c => c.Kind == kind, ct);

    public Task<Channel?> FindForUpdateAsync(Guid officeId, string kind, CancellationToken ct = default)
        => Scoped(officeId).SingleOrDefaultAsync(c => c.Kind == kind, ct);

    public async Task<IReadOnlyList<Channel>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().ToListAsync(ct);

    public Task AddAsync(Channel channel, CancellationToken ct = default)
    {
        db.Channels.Add(channel);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    private IQueryable<Channel> Scoped(Guid officeId) => db.Channels.Where(c => c.OfficeId == officeId);
}
