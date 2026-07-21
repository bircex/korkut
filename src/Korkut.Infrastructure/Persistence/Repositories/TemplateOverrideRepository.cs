using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Templates;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF/Postgres adapter for <see cref="ITemplateOverrideRepository"/>. Holds only the
/// customizations that take precedence over the static catalog defaults. Tenant context
/// is set by the caller (RLS).
/// </summary>
public sealed class TemplateOverrideRepository(KorkutDbContext db) : ITemplateOverrideRepository
{
    public async Task<IReadOnlyList<TemplateOverride>> ListAsync(Guid officeId, string kind, CancellationToken ct = default)
        => (await db.TemplateOverrides.AsNoTracking()
                .Where(x => x.OfficeId == officeId && x.Kind == kind).ToListAsync(ct))
            .Select(x => new TemplateOverride(x.Scope, x.UserId, x.Kind, x.TypeId, x.Content)).ToList();

    public Task<string?> GetContentAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, CancellationToken ct = default)
        => db.TemplateOverrides.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.Scope == scope && x.UserId == userId && x.Kind == kind && x.TypeId == typeId)
            .Select(x => x.Content).SingleOrDefaultAsync(ct);

    public async Task SaveAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, string content, DateTimeOffset updatedAt, CancellationToken ct = default)
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
        row.UpdatedAt = updatedAt;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, CancellationToken ct = default)
        => await db.TemplateOverrides
            .Where(x => x.OfficeId == officeId && x.Scope == scope && x.UserId == userId && x.Kind == kind && x.TypeId == typeId)
            .ExecuteDeleteAsync(ct) > 0;
}
