using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// Correspondence-register ("evrak") CRUD. Tenant context is set by the caller (RLS).
/// A register row is metadata about an official document, optionally filtered by
/// direction (incoming/outgoing/internal).
/// </summary>
public sealed class CorrespondenceStore(KorkutDbContext db, IClock clock)
{
    public async Task<Guid> CreateAsync(
        Guid officeId, string direction, string title, string? referenceNo, DateOnly? docDate,
        Guid? matterId, string? notes, CancellationToken ct = default)
    {
        var row = new CorrespondenceEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Direction = direction,
            Title = title,
            ReferenceNo = referenceNo,
            DocDate = docDate,
            MatterId = matterId,
            Notes = notes,
            CreatedAt = clock.UtcNow,
        };
        db.Correspondence.Add(row);
        await db.SaveChangesAsync(ct);
        return row.Id;
    }

    public Task<List<CorrespondenceEntity>> ListAsync(Guid officeId, string? direction = null, CancellationToken ct = default)
        => db.Correspondence.AsNoTracking()
            .Where(x => x.OfficeId == officeId && (direction == null || x.Direction == direction))
            .OrderByDescending(x => x.DocDate ?? DateOnly.FromDateTime(x.CreatedAt.UtcDateTime))
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public Task<CorrespondenceEntity?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => db.Correspondence.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id, ct);

    public async Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.Correspondence.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;
}
