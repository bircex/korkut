using Korkut.Application.Invites;
using Korkut.Domain.Invites;
using Korkut.Infrastructure.Common;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Invites;

/// <summary>
/// EF/Postgres adapter for <see cref="IInviteRepository"/>. Listing shows only what an
/// owner can still act on: pending invites, newest first.
/// </summary>
public sealed class InviteRepository(KorkutDbContext db) : Repository<Invite>(db), IInviteRepository
{
    public override async Task<IReadOnlyList<Invite>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(i => i.AcceptedAt == null)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
}
