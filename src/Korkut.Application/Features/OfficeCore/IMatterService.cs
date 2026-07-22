using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.OfficeCore;
using Korkut.Domain.Ids;
using Korkut.Domain.Time;

namespace Korkut.Application.Features.OfficeCore;

/// <summary>A matter plus its hearings — the detail read model.</summary>
public sealed record MatterDetail(Matter Matter, IReadOnlyList<Hearing> Hearings);

/// <summary>
/// Matter (dosya) + hearing use-cases. Owns the court-title snapshot rule: linking a
/// court captures its current title onto the matter, so a later rename never rewrites
/// the historical text.
/// </summary>
public interface IMatterService
{
    Task<Matter> CreateAsync(Guid officeId, string title, Guid? clientId, Guid? courtId, CancellationToken ct = default);

    Task<IReadOnlyList<Matter>> ListAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>The matter with its hearings, or null if missing/soft-deleted.</summary>
    Task<MatterDetail?> GetDetailAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>Add a hearing to a matter; returns null if the matter is missing.</summary>
    Task<Hearing?> AddHearingAsync(Guid officeId, Guid matterId, DateTimeOffset scheduledAt, string? type, string? location, string? notes, CancellationToken ct = default);
}

/// <summary>Matter/hearing orchestration over the repository ports.</summary>
public sealed class MatterService(IMatterRepository matters, IHearingRepository hearings, IClock clock) : IMatterService
{
    public async Task<Matter> CreateAsync(Guid officeId, string title, Guid? clientId, Guid? courtId, CancellationToken ct = default)
    {
        var courtName = courtId is { } cid ? await matters.GetCourtNameAsync(officeId, cid, ct) : null;
        var now = clock.UtcNow;
        var matter = new Matter(UuidV7.NewId(), officeId, title, clientId, null, null, null, courtId, courtName, "open", now, null, now);
        return await matters.AddAsync(matter, ct);
    }

    public Task<IReadOnlyList<Matter>> ListAsync(Guid officeId, CancellationToken ct = default)
        => matters.ListAsync(officeId, ct);

    public async Task<MatterDetail?> GetDetailAsync(Guid officeId, Guid id, CancellationToken ct = default)
    {
        var matter = await matters.GetAsync(officeId, id, ct);
        if (matter is null)
        {
            return null;
        }

        var list = await hearings.ListForMatterAsync(officeId, id, ct);
        return new MatterDetail(matter, list);
    }

    public Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => matters.SoftDeleteAsync(officeId, id, clock.UtcNow, ct);

    public async Task<Hearing?> AddHearingAsync(Guid officeId, Guid matterId, DateTimeOffset scheduledAt, string? type, string? location, string? notes, CancellationToken ct = default)
    {
        // The matter must exist (and be active) — the composite FK also enforces tenant scope.
        if (await matters.GetAsync(officeId, matterId, ct) is null)
        {
            return null;
        }

        var hearing = new Hearing(UuidV7.NewId(), officeId, matterId, scheduledAt, type, location, null, notes, "pending");
        return await hearings.AddAsync(hearing, ct);
    }
}
