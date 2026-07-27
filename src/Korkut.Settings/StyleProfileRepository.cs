using Microsoft.EntityFrameworkCore;

namespace Korkut.Settings;

/// <summary>
/// EF/Postgres adapter for <see cref="IStyleProfileRepository"/> over the ambient DbContext. A profile
/// is looked up by its layer — (office, scope, user), the unique index behind it — rather than by id.
/// </summary>
public sealed class StyleProfileRepository(DbContext db) : IStyleProfileRepository
{
    public Task<StyleProfile?> FindAsync(Guid officeId, string scope, Guid userId, CancellationToken ct = default)
        => Layer(officeId, scope, userId).AsNoTracking().SingleOrDefaultAsync(ct);

    public Task<StyleProfile?> FindForUpdateAsync(Guid officeId, string scope, Guid userId, CancellationToken ct = default)
        => Layer(officeId, scope, userId).SingleOrDefaultAsync(ct);

    public Task AddAsync(StyleProfile profile, CancellationToken ct = default)
    {
        db.Set<StyleProfile>().Add(profile);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    private IQueryable<StyleProfile> Layer(Guid officeId, string scope, Guid userId)
        => db.Set<StyleProfile>().Where(p => p.OfficeId == officeId && p.Scope == scope && p.UserId == userId);
}
