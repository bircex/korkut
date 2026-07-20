using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// Office-core domain operations (client/matter/party/court). Enforces the
/// historical-snapshot rule: when a court is linked to a matter, the court's CURRENT
/// display title is captured onto the matter, so a later court rename never rewrites
/// the matter's historical text (KDM-023). Tenant context is set by the caller (RLS).
/// </summary>
public sealed class OfficeCoreStore(KorkutDbContext db, IClock clock)
{
    public async Task<Guid> CreateMatterAsync(
        Guid officeId, string title, Guid? clientId = null, Guid? courtId = null, CancellationToken ct = default)
    {
        var matter = new MatterEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Title = title,
            ClientId = clientId,
            Status = "open",
            OpenedAt = clock.UtcNow,
            CreatedAt = clock.UtcNow,
        };
        if (courtId is { } cid)
        {
            matter.CourtId = cid;
            matter.CourtNameSnapshot = await CourtNameAsync(officeId, cid, ct);
        }

        db.Matters.Add(matter);
        await db.SaveChangesAsync(ct);
        return matter.Id;
    }

    /// <summary>Link/relink a court, snapshotting its current title onto the matter.</summary>
    public async Task LinkCourtAsync(Guid officeId, Guid matterId, Guid courtId, CancellationToken ct = default)
    {
        var snapshot = await CourtNameAsync(officeId, courtId, ct);
        await db.Matters.Where(x => x.OfficeId == officeId && x.Id == matterId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.CourtId, courtId)
                .SetProperty(x => x.CourtNameSnapshot, snapshot), ct);
    }

    /// <summary>Active matters only (soft-deleted rows are excluded from listings).</summary>
    public Task<List<MatterEntity>> ListMattersAsync(Guid officeId, CancellationToken ct = default)
        => db.Matters.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.DeletedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public Task SoftDeleteMatterAsync(Guid officeId, Guid matterId, CancellationToken ct = default)
        => db.Matters.Where(x => x.OfficeId == officeId && x.Id == matterId && x.DeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DeletedAt, clock.UtcNow), ct);

    private async Task<string?> CourtNameAsync(Guid officeId, Guid courtId, CancellationToken ct)
        => await db.Courts.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.Id == courtId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct);
}
