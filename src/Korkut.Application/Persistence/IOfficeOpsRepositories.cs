using Korkut.Domain.OfficeOps;

namespace Korkut.Application.Abstractions.Persistence;

/// <summary>Persistence port for tasks (see IInvoiceRepository for the contract shape).</summary>
public interface ITaskRepository
{
    Task<WorkTask> AddAsync(WorkTask task, CancellationToken ct = default);

    Task<IReadOnlyList<WorkTask>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);

    Task<WorkTask?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>Set status + completion timestamp (the service decides the timestamp). False if missing.</summary>
    Task<bool> SetStatusAsync(Guid officeId, Guid id, string status, DateTimeOffset? completedAt, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default);
}

/// <summary>Persistence port for deadlines.</summary>
public interface IDeadlineRepository
{
    Task<Deadline> AddAsync(Deadline deadline, CancellationToken ct = default);

    Task<IReadOnlyList<Deadline>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);

    /// <summary>Stamp completion (only if not already completed). False if missing/already done.</summary>
    Task<bool> CompleteAsync(Guid officeId, Guid id, DateTimeOffset completedAt, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default);
}

/// <summary>Persistence port for communication notes.</summary>
public interface INoteRepository
{
    Task<CommunicationNote> AddAsync(CommunicationNote note, CancellationToken ct = default);

    Task<IReadOnlyList<CommunicationNote>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);
}
