using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.OfficeCore;
using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>EF/Postgres adapter for <see cref="IMatterRepository"/>.</summary>
public sealed class MatterRepository(KorkutDbContext db) : IMatterRepository
{
    public async Task<Matter> AddAsync(Matter matter, CancellationToken ct = default)
    {
        db.Matters.Add(new MatterEntity
        {
            Id = matter.Id, OfficeId = matter.OfficeId, Title = matter.Title, ClientId = matter.ClientId,
            FileNo = matter.FileNo, CaseType = matter.CaseType, Counterparty = matter.Counterparty,
            CourtId = matter.CourtId, CourtNameSnapshot = matter.CourtNameSnapshot, Status = matter.Status,
            OpenedAt = matter.OpenedAt, DeletedAt = matter.DeletedAt, CreatedAt = matter.CreatedAt,
        });
        await db.SaveChangesAsync(ct);
        return matter;
    }

    public async Task<IReadOnlyList<Matter>> ListAsync(Guid officeId, CancellationToken ct = default)
        => (await db.Matters.AsNoTracking()
                .Where(x => x.OfficeId == officeId && x.DeletedAt == null).OrderByDescending(x => x.CreatedAt).ToListAsync(ct))
            .Select(ToDomain).ToList();

    public async Task<Matter?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
    {
        var e = await db.Matters.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id && x.DeletedAt == null, ct);
        return e is null ? null : ToDomain(e);
    }

    public async Task<bool> SoftDeleteAsync(Guid officeId, Guid id, DateTimeOffset deletedAt, CancellationToken ct = default)
        => await db.Matters.Where(x => x.OfficeId == officeId && x.Id == id && x.DeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DeletedAt, deletedAt), ct) > 0;

    public Task<string?> GetCourtNameAsync(Guid officeId, Guid courtId, CancellationToken ct = default)
        => db.Courts.AsNoTracking().Where(x => x.OfficeId == officeId && x.Id == courtId).Select(x => x.Name).FirstOrDefaultAsync(ct);

    private static Matter ToDomain(MatterEntity e) =>
        new(e.Id, e.OfficeId, e.Title, e.ClientId, e.FileNo, e.CaseType, e.Counterparty, e.CourtId,
            e.CourtNameSnapshot, e.Status, e.OpenedAt, e.DeletedAt, e.CreatedAt);
}

/// <summary>EF/Postgres adapter for <see cref="IHearingRepository"/>.</summary>
public sealed class HearingRepository(KorkutDbContext db) : IHearingRepository
{
    public async Task<Hearing> AddAsync(Hearing hearing, CancellationToken ct = default)
    {
        db.MatterHearings.Add(new MatterHearingEntity
        {
            Id = hearing.Id, OfficeId = hearing.OfficeId, MatterId = hearing.MatterId, ScheduledAt = hearing.ScheduledAt,
            Type = hearing.Type, Location = hearing.Location, Result = hearing.Result, Notes = hearing.Notes,
            ReminderStatus = hearing.ReminderStatus,
        });
        await db.SaveChangesAsync(ct);
        return hearing;
    }

    public async Task<IReadOnlyList<Hearing>> ListForMatterAsync(Guid officeId, Guid matterId, CancellationToken ct = default)
        => (await db.MatterHearings.AsNoTracking()
                .Where(x => x.OfficeId == officeId && x.MatterId == matterId).OrderBy(x => x.ScheduledAt).ToListAsync(ct))
            .Select(ToDomain).ToList();

    private static Hearing ToDomain(MatterHearingEntity e) =>
        new(e.Id, e.OfficeId, e.MatterId, e.ScheduledAt, e.Type, e.Location, e.Result, e.Notes, e.ReminderStatus);
}
