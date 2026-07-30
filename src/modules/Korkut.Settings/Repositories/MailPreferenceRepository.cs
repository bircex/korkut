using Korkut.Settings.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Settings.Repositories;

/// <summary>EF/Postgres adapter for <see cref="IMailPreferenceRepository"/> over the ambient DbContext (office-keyed).</summary>
internal sealed class MailPreferenceRepository(DbContext db) : IMailPreferenceRepository
{
    public Task<MailPreference?> FindAsync(Guid officeId, CancellationToken ct = default)
        => db.Set<MailPreference>().AsNoTracking().SingleOrDefaultAsync(p => p.OfficeId == officeId, ct);

    public Task<MailPreference?> FindForUpdateAsync(Guid officeId, CancellationToken ct = default)
        => db.Set<MailPreference>().SingleOrDefaultAsync(p => p.OfficeId == officeId, ct);

    public Task AddAsync(MailPreference preference, CancellationToken ct = default)
    {
        db.Set<MailPreference>().Add(preference);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
