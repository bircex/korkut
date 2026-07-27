using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization;

/// <summary>
/// EF/Postgres adapter for <see cref="IInviteRepository"/> over the ambient DbContext. Listing shows
/// only what an owner can still act on: pending invites, newest first.
/// </summary>
public sealed class InviteRepository(DbContext db) : Repository<Invite>(db), IInviteRepository
{
    public override async Task<IReadOnlyList<Invite>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(i => i.AcceptedAt == null)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
}
