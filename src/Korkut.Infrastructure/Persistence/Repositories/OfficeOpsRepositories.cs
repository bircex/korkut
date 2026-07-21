using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.OfficeOps;
using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>EF/Postgres adapter for <see cref="ITaskRepository"/>.</summary>
public sealed class TaskRepository(KorkutDbContext db) : ITaskRepository
{
    public async Task<WorkTask> AddAsync(WorkTask task, CancellationToken ct = default)
    {
        db.Tasks.Add(new TaskEntity
        {
            Id = task.Id, OfficeId = task.OfficeId, MatterId = task.MatterId, AssigneeId = task.AssigneeId,
            Title = task.Title, Status = task.Status, DueAt = task.DueAt, CompletedAt = task.CompletedAt, CreatedAt = task.CreatedAt,
        });
        await db.SaveChangesAsync(ct);
        return task;
    }

    public async Task<IReadOnlyList<WorkTask>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => (await db.Tasks.AsNoTracking()
                .Where(x => x.OfficeId == officeId && (matterId == null || x.MatterId == matterId))
                .OrderBy(x => x.Status == "done").ThenBy(x => x.DueAt ?? DateTimeOffset.MaxValue)
                .ToListAsync(ct))
            .Select(ToDomain).ToList();

    public async Task<WorkTask?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
    {
        var e = await db.Tasks.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id, ct);
        return e is null ? null : ToDomain(e);
    }

    public async Task<bool> SetStatusAsync(Guid officeId, Guid id, string status, DateTimeOffset? completedAt, CancellationToken ct = default)
        => await db.Tasks.Where(x => x.OfficeId == officeId && x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status).SetProperty(x => x.CompletedAt, completedAt), ct) > 0;

    public async Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.Tasks.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    private static WorkTask ToDomain(TaskEntity e) =>
        new(e.Id, e.OfficeId, e.MatterId, e.AssigneeId, e.Title, e.Status, e.DueAt, e.CompletedAt, e.CreatedAt);
}

/// <summary>EF/Postgres adapter for <see cref="IDeadlineRepository"/>.</summary>
public sealed class DeadlineRepository(KorkutDbContext db) : IDeadlineRepository
{
    public async Task<Deadline> AddAsync(Deadline deadline, CancellationToken ct = default)
    {
        db.Deadlines.Add(new DeadlineEntity
        {
            Id = deadline.Id, OfficeId = deadline.OfficeId, MatterId = deadline.MatterId, Kind = deadline.Kind,
            Title = deadline.Title, DueDate = deadline.DueDate, TimeZoneId = deadline.TimeZoneId,
            ReminderDaysBefore = deadline.ReminderDaysBefore, CompletedAt = deadline.CompletedAt, CreatedAt = deadline.CreatedAt,
        });
        await db.SaveChangesAsync(ct);
        return deadline;
    }

    public async Task<IReadOnlyList<Deadline>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => (await db.Deadlines.AsNoTracking()
                .Where(x => x.OfficeId == officeId && (matterId == null || x.MatterId == matterId))
                .OrderBy(x => x.DueDate)
                .ToListAsync(ct))
            .Select(ToDomain).ToList();

    public async Task<bool> CompleteAsync(Guid officeId, Guid id, DateTimeOffset completedAt, CancellationToken ct = default)
        => await db.Deadlines.Where(x => x.OfficeId == officeId && x.Id == id && x.CompletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CompletedAt, completedAt), ct) > 0;

    public async Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.Deadlines.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    private static Deadline ToDomain(DeadlineEntity e) =>
        new(e.Id, e.OfficeId, e.MatterId, e.Kind, e.Title, e.DueDate, e.TimeZoneId, e.ReminderDaysBefore, e.CompletedAt, e.CreatedAt);
}

/// <summary>EF/Postgres adapter for <see cref="INoteRepository"/>.</summary>
public sealed class NoteRepository(KorkutDbContext db) : INoteRepository
{
    public async Task<CommunicationNote> AddAsync(CommunicationNote note, CancellationToken ct = default)
    {
        db.CommunicationNotes.Add(new CommunicationNoteEntity
        {
            Id = note.Id, OfficeId = note.OfficeId, MatterId = note.MatterId, ClientId = note.ClientId,
            Channel = note.Channel, Body = note.Body, OccurredAt = note.OccurredAt, CreatedAt = note.CreatedAt,
        });
        await db.SaveChangesAsync(ct);
        return note;
    }

    public async Task<IReadOnlyList<CommunicationNote>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => (await db.CommunicationNotes.AsNoTracking()
                .Where(x => x.OfficeId == officeId && (matterId == null || x.MatterId == matterId))
                .OrderByDescending(x => x.OccurredAt)
                .ToListAsync(ct))
            .Select(ToDomain).ToList();

    private static CommunicationNote ToDomain(CommunicationNoteEntity e) =>
        new(e.Id, e.OfficeId, e.MatterId, e.ClientId, e.Channel, e.Body, e.OccurredAt, e.CreatedAt);
}
