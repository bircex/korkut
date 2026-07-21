using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Correspondence;
using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>EF/Postgres adapter for <see cref="ICorrespondenceRepository"/>; maps entity ↔ domain model.</summary>
public sealed class CorrespondenceRepository(KorkutDbContext db) : ICorrespondenceRepository
{
    public async Task<CorrespondenceItem> AddAsync(CorrespondenceItem item, CancellationToken ct = default)
    {
        db.Correspondence.Add(ToEntity(item));
        await db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<IReadOnlyList<CorrespondenceItem>> ListAsync(Guid officeId, string? direction, CancellationToken ct = default)
        => (await db.Correspondence.AsNoTracking()
                .Where(x => x.OfficeId == officeId && (direction == null || x.Direction == direction))
                .OrderByDescending(x => x.DocDate ?? DateOnly.FromDateTime(x.CreatedAt.UtcDateTime))
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync(ct))
            .Select(ToDomain).ToList();

    public async Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.Correspondence.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    private static CorrespondenceItem ToDomain(CorrespondenceEntity e) =>
        new(e.Id, e.OfficeId, e.MatterId, e.Direction, e.Title, e.ReferenceNo, e.DocDate, e.Notes, e.CreatedAt);

    private static CorrespondenceEntity ToEntity(CorrespondenceItem c) => new()
    {
        Id = c.Id, OfficeId = c.OfficeId, MatterId = c.MatterId, Direction = c.Direction, Title = c.Title,
        ReferenceNo = c.ReferenceNo, DocDate = c.DocDate, Notes = c.Notes, CreatedAt = c.CreatedAt,
    };
}
