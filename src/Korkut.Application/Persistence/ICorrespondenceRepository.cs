using Korkut.Domain.Correspondence;

namespace Korkut.Application.Abstractions.Persistence;

/// <summary>Persistence port for the correspondence register. Speaks in domain models (see IInvoiceRepository).</summary>
public interface ICorrespondenceRepository
{
    Task<CorrespondenceItem> AddAsync(CorrespondenceItem item, CancellationToken ct = default);

    /// <summary>Register entries for the office, newest first, optionally filtered by direction.</summary>
    Task<IReadOnlyList<CorrespondenceItem>> ListAsync(Guid officeId, string? direction, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default);
}
