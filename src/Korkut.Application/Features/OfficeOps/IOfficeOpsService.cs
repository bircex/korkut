using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.OfficeOps;
using Korkut.Domain.Ids;
using Korkut.Domain.Time;

namespace Korkut.Application.Features.OfficeOps;

/// <summary>Office-operations use-cases (tasks, deadlines, communication notes).</summary>
public interface IOfficeOpsService
{
    Task<WorkTask> CreateTaskAsync(Guid officeId, string title, Guid? matterId, Guid? assigneeId, DateTimeOffset? dueAt, CancellationToken ct = default);

    Task<IReadOnlyList<WorkTask>> ListTasksAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);

    Task<WorkTask?> SetTaskStatusAsync(Guid officeId, Guid id, string status, CancellationToken ct = default);

    Task<bool> DeleteTaskAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<Deadline> CreateDeadlineAsync(Guid officeId, string title, DateOnly dueDate, string kind, Guid? matterId, string timeZoneId, int reminderDaysBefore, CancellationToken ct = default);

    Task<IReadOnlyList<Deadline>> ListDeadlinesAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);

    Task<bool> CompleteDeadlineAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<bool> DeleteDeadlineAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<CommunicationNote> CreateNoteAsync(Guid officeId, string body, string channel, Guid? matterId, Guid? clientId, DateTimeOffset? occurredAt, CancellationToken ct = default);

    Task<IReadOnlyList<CommunicationNote>> ListNotesAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);
}

/// <summary>
/// Office-ops orchestration over the repository ports. Owns the completion rules — a
/// "done" task stamps its completion time; a deadline completes only once — with no
/// storage knowledge.
/// </summary>
public sealed class OfficeOpsService(ITaskRepository tasks, IDeadlineRepository deadlines, INoteRepository notes, IClock clock) : IOfficeOpsService
{
    public Task<WorkTask> CreateTaskAsync(Guid officeId, string title, Guid? matterId, Guid? assigneeId, DateTimeOffset? dueAt, CancellationToken ct = default)
        => tasks.AddAsync(new WorkTask(UuidV7.NewId(), officeId, matterId, assigneeId, title, "open", dueAt, null, clock.UtcNow), ct);

    public Task<IReadOnlyList<WorkTask>> ListTasksAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => tasks.ListAsync(officeId, matterId, ct);

    public async Task<WorkTask?> SetTaskStatusAsync(Guid officeId, Guid id, string status, CancellationToken ct = default)
    {
        var completedAt = status == "done" ? clock.UtcNow : (DateTimeOffset?)null;
        return await tasks.SetStatusAsync(officeId, id, status, completedAt, ct) ? await tasks.GetAsync(officeId, id, ct) : null;
    }

    public Task<bool> DeleteTaskAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => tasks.DeleteAsync(officeId, id, ct);

    public Task<Deadline> CreateDeadlineAsync(Guid officeId, string title, DateOnly dueDate, string kind, Guid? matterId, string timeZoneId, int reminderDaysBefore, CancellationToken ct = default)
        => deadlines.AddAsync(new Deadline(UuidV7.NewId(), officeId, matterId, kind, title, dueDate, timeZoneId, reminderDaysBefore, null, clock.UtcNow), ct);

    public Task<IReadOnlyList<Deadline>> ListDeadlinesAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => deadlines.ListAsync(officeId, matterId, ct);

    public Task<bool> CompleteDeadlineAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => deadlines.CompleteAsync(officeId, id, clock.UtcNow, ct);

    public Task<bool> DeleteDeadlineAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => deadlines.DeleteAsync(officeId, id, ct);

    public Task<CommunicationNote> CreateNoteAsync(Guid officeId, string body, string channel, Guid? matterId, Guid? clientId, DateTimeOffset? occurredAt, CancellationToken ct = default)
        => notes.AddAsync(new CommunicationNote(UuidV7.NewId(), officeId, matterId, clientId, channel, body, occurredAt ?? clock.UtcNow, clock.UtcNow), ct);

    public Task<IReadOnlyList<CommunicationNote>> ListNotesAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => notes.ListAsync(officeId, matterId, ct);
}
