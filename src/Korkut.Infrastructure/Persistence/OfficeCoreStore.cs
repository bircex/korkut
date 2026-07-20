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

    public Task<MatterEntity?> GetMatterAsync(Guid officeId, Guid matterId, CancellationToken ct = default)
        => db.Matters.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == matterId && x.DeletedAt == null, ct);

    // --- Clients ---

    public async Task<Guid> CreateClientAsync(
        Guid officeId, string name, string kind, string? email, string? phone, CancellationToken ct = default)
    {
        var client = new ClientEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Name = name,
            Kind = kind,
            Email = email,
            Phone = phone,
            CreatedAt = clock.UtcNow,
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);
        return client.Id;
    }

    public Task<List<ClientEntity>> ListClientsAsync(Guid officeId, CancellationToken ct = default)
        => db.Clients.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.DeletedAt == null)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public Task<ClientEntity?> GetClientAsync(Guid officeId, Guid clientId, CancellationToken ct = default)
        => db.Clients.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == clientId && x.DeletedAt == null, ct);

    /// <summary>Update mutable client fields. Returns false if the client doesn't exist.</summary>
    public async Task<bool> UpdateClientAsync(
        Guid officeId, Guid clientId, string name, string kind, string? email, string? phone, CancellationToken ct = default)
    {
        var affected = await db.Clients
            .Where(x => x.OfficeId == officeId && x.Id == clientId && x.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Name, name)
                .SetProperty(x => x.Kind, kind)
                .SetProperty(x => x.Email, email)
                .SetProperty(x => x.Phone, phone), ct);
        return affected > 0;
    }

    public async Task<bool> SoftDeleteClientAsync(Guid officeId, Guid clientId, CancellationToken ct = default)
    {
        var affected = await db.Clients
            .Where(x => x.OfficeId == officeId && x.Id == clientId && x.DeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DeletedAt, clock.UtcNow), ct);
        return affected > 0;
    }

    private async Task<string?> CourtNameAsync(Guid officeId, Guid courtId, CancellationToken ct)
        => await db.Courts.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.Id == courtId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct);
}
