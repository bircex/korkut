using Korkut.Application.Settings;
using Korkut.Domain.Settings;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Settings;

/// <summary>
/// EF/Postgres adapter for <see cref="IStyleProfileRepository"/>. A profile is looked up by
/// its layer — (office, scope, user), the unique index behind it — rather than by id.
/// </summary>
public sealed class StyleProfileRepository(KorkutDbContext db) : IStyleProfileRepository
{
    public Task<StyleProfile?> FindAsync(Guid officeId, string scope, Guid userId, CancellationToken ct = default)
        => Layer(officeId, scope, userId).AsNoTracking().SingleOrDefaultAsync(ct);

    public Task<StyleProfile?> FindForUpdateAsync(Guid officeId, string scope, Guid userId, CancellationToken ct = default)
        => Layer(officeId, scope, userId).SingleOrDefaultAsync(ct);

    public Task AddAsync(StyleProfile profile, CancellationToken ct = default)
    {
        db.StyleProfiles.Add(profile);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    private IQueryable<StyleProfile> Layer(Guid officeId, string scope, Guid userId)
        => db.StyleProfiles.Where(p => p.OfficeId == officeId && p.Scope == scope && p.UserId == userId);
}
