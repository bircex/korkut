using Korkut.Deadlines.Repositories;
using Korkut.Deadlines.Entities;
using Korkut.Contract.Deadlines;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Deadlines.Services;

/// <summary>
/// Implements the cross-module <see cref="IDeadlineLookup"/> over the deadline repository, reading in
/// the caller's already-open tenant scope. Lets the calendar list deadlines without the Deadline type.
/// </summary>
internal sealed class DeadlineLookup(IDeadlineRepository repository, DbContext db) : IDeadlineLookup
{
    private static DateTimeOffset AtMidnightUtc(DateOnly d) => new(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public async Task<IReadOnlyList<DeadlineResponse>> ListInRangeAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, Guid? matterId, CancellationToken ct = default)
        => (await repository.ListAsync(officeId, matterId, ct))
            .Where(d => (start == null || AtMidnightUtc(d.DueDate) >= start)
                && (end == null || AtMidnightUtc(d.DueDate) <= end))
            .Select(ToResponse)
            .ToList();

    private static DeadlineResponse ToResponse(Deadline d) =>
        new(d.Id, d.Title, d.DueDate, d.Kind, d.CompletedAt is null ? "open" : "done", d.MatterId, d.Notes,
            d.TimeZoneId, d.ReminderDaysBefore, d.CompletedAt, d.CreatedAt);

    public async Task<DeadlineCounts> CountsAsync(Guid officeId, DateOnly today, DateOnly until, CancellationToken ct = default)
    {
        var open = db.Set<Deadline>().AsNoTracking().Where(d => d.OfficeId == officeId && d.CompletedAt == null);
        var upcoming = await open.CountAsync(d => d.DueDate >= today && d.DueDate <= until, ct);
        var overdue = await open.CountAsync(d => d.DueDate < today, ct);
        return new DeadlineCounts(upcoming, overdue);
    }
}
