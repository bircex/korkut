using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// Office operations CRUD (tasks, deadlines, communication notes). Tenant context is
/// set by the caller (RLS). Completion is modelled as a state transition (a completed
/// task/deadline keeps its row for the audit trail rather than being deleted).
/// </summary>
public sealed class OfficeOpsStore(KorkutDbContext db, IClock clock)
{
    // --- Tasks ---

    public async Task<Guid> CreateTaskAsync(
        Guid officeId, string title, Guid? matterId, Guid? assigneeId, DateTimeOffset? dueAt, CancellationToken ct = default)
    {
        var task = new TaskEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Title = title,
            MatterId = matterId,
            AssigneeId = assigneeId,
            DueAt = dueAt,
            Status = "open",
            CreatedAt = clock.UtcNow,
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        return task.Id;
    }

    public Task<List<TaskEntity>> ListTasksAsync(Guid officeId, Guid? matterId = null, CancellationToken ct = default)
        => db.Tasks.AsNoTracking()
            .Where(x => x.OfficeId == officeId && (matterId == null || x.MatterId == matterId))
            .OrderBy(x => x.Status == "done").ThenBy(x => x.DueAt ?? DateTimeOffset.MaxValue)
            .ToListAsync(ct);

    public Task<TaskEntity?> GetTaskAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => db.Tasks.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id, ct);

    /// <summary>Set a task's status; "done" stamps the completion time. Returns false if missing.</summary>
    public async Task<bool> SetTaskStatusAsync(Guid officeId, Guid id, string status, CancellationToken ct = default)
    {
        var completedAt = status == "done" ? (DateTimeOffset?)clock.UtcNow : null;
        var affected = await db.Tasks
            .Where(x => x.OfficeId == officeId && x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status).SetProperty(x => x.CompletedAt, completedAt), ct);
        return affected > 0;
    }

    public async Task<bool> DeleteTaskAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.Tasks.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    // --- Deadlines ---

    public async Task<Guid> CreateDeadlineAsync(
        Guid officeId, string title, DateOnly dueDate, string kind, Guid? matterId, string timeZoneId, int reminderDaysBefore, CancellationToken ct = default)
    {
        var deadline = new DeadlineEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Title = title,
            DueDate = dueDate,
            Kind = kind,
            MatterId = matterId,
            TimeZoneId = timeZoneId,
            ReminderDaysBefore = reminderDaysBefore,
            CreatedAt = clock.UtcNow,
        };
        db.Deadlines.Add(deadline);
        await db.SaveChangesAsync(ct);
        return deadline.Id;
    }

    public Task<List<DeadlineEntity>> ListDeadlinesAsync(Guid officeId, Guid? matterId = null, CancellationToken ct = default)
        => db.Deadlines.AsNoTracking()
            .Where(x => x.OfficeId == officeId && (matterId == null || x.MatterId == matterId))
            .OrderBy(x => x.DueDate)
            .ToListAsync(ct);

    public async Task<bool> CompleteDeadlineAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.Deadlines.Where(x => x.OfficeId == officeId && x.Id == id && x.CompletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CompletedAt, clock.UtcNow), ct) > 0;

    public async Task<bool> DeleteDeadlineAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.Deadlines.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    // --- Communication notes ---

    public async Task<Guid> CreateNoteAsync(
        Guid officeId, string body, string channel, Guid? matterId, Guid? clientId, DateTimeOffset? occurredAt, CancellationToken ct = default)
    {
        var note = new CommunicationNoteEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Body = body,
            Channel = channel,
            MatterId = matterId,
            ClientId = clientId,
            OccurredAt = occurredAt ?? clock.UtcNow,
            CreatedAt = clock.UtcNow,
        };
        db.CommunicationNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return note.Id;
    }

    public Task<List<CommunicationNoteEntity>> ListNotesAsync(Guid officeId, Guid? matterId = null, CancellationToken ct = default)
        => db.CommunicationNotes.AsNoTracking()
            .Where(x => x.OfficeId == officeId && (matterId == null || x.MatterId == matterId))
            .OrderByDescending(x => x.OccurredAt)
            .ToListAsync(ct);
}
