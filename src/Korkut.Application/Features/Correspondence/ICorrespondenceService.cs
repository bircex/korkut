using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Correspondence;
using Korkut.Domain.Ids;
using Korkut.Domain.Time;

namespace Korkut.Application.Features.Correspondence;

/// <summary>Correspondence-register ("evrak") use-cases. Caller supplies the tenant-scoped office id.</summary>
public interface ICorrespondenceService
{
    Task<CorrespondenceItem> CreateAsync(
        Guid officeId, string direction, string title, string? referenceNo, DateOnly? docDate,
        Guid? matterId, string? notes, CancellationToken ct = default);

    Task<IReadOnlyList<CorrespondenceItem>> ListAsync(Guid officeId, string? direction, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default);
}

/// <summary>Correspondence orchestration over the repository port (id/time stamping).</summary>
public sealed class CorrespondenceService(ICorrespondenceRepository repo, IClock clock) : ICorrespondenceService
{
    public Task<CorrespondenceItem> CreateAsync(
        Guid officeId, string direction, string title, string? referenceNo, DateOnly? docDate,
        Guid? matterId, string? notes, CancellationToken ct = default)
    {
        var item = new CorrespondenceItem(UuidV7.NewId(), officeId, matterId, direction, title, referenceNo, docDate, notes, clock.UtcNow);
        return repo.AddAsync(item, ct);
    }

    public Task<IReadOnlyList<CorrespondenceItem>> ListAsync(Guid officeId, string? direction, CancellationToken ct = default)
        => repo.ListAsync(officeId, direction, ct);

    public Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => repo.DeleteAsync(officeId, id, ct);
}
