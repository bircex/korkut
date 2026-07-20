using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// Per-office/user overrides of built-in document templates. Tenant context is set by
/// the caller (RLS). The built-in defaults live in the static catalog; this store only
/// holds the customizations that take precedence over them.
/// </summary>
public sealed class TemplateOverrideStore(KorkutDbContext db, IClock clock)
{
    public const string OfficeScope = "office";
    public const string UserScope = "user";

    /// <summary>All overrides for the office (both scopes) — for computing has-override flags in a listing.</summary>
    public Task<List<TemplateOverrideEntity>> ListAsync(Guid officeId, string kind, CancellationToken ct = default)
        => db.TemplateOverrides.AsNoTracking().Where(x => x.OfficeId == officeId && x.Kind == kind).ToListAsync(ct);

    public Task<TemplateOverrideEntity?> GetAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, CancellationToken ct = default)
        => db.TemplateOverrides.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Scope == scope && x.UserId == userId && x.Kind == kind && x.TypeId == typeId, ct);

    public async Task SaveAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, string content, CancellationToken ct = default)
    {
        var row = await db.TemplateOverrides
            .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Scope == scope && x.UserId == userId && x.Kind == kind && x.TypeId == typeId, ct);
        if (row is null)
        {
            row = new TemplateOverrideEntity
            {
                Id = UuidV7.NewId(), OfficeId = officeId, Scope = scope, UserId = userId, Kind = kind, TypeId = typeId,
            };
            db.TemplateOverrides.Add(row);
        }

        row.Content = content;
        row.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, CancellationToken ct = default)
        => await db.TemplateOverrides
            .Where(x => x.OfficeId == officeId && x.Scope == scope && x.UserId == userId && x.Kind == kind && x.TypeId == typeId)
            .ExecuteDeleteAsync(ct) > 0;
}
