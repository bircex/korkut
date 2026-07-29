using Korkut.Deadlines.Repositories;
using Korkut.Deadlines.Entities;
using Korkut.Contract.Deadlines;

namespace Korkut.Deadlines.Services;

/// <summary>
/// Implements the cross-module <see cref="IDeadlineLookup"/> over the deadline repository, reading in
/// the caller's already-open tenant scope. Lets the calendar list deadlines without the Deadline type.
/// </summary>
internal sealed class DeadlineLookup(IDeadlineRepository repository) : IDeadlineLookup
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
        new(d.Id, d.Title, d.DueDate, d.Kind, d.MatterId, d.TimeZoneId, d.ReminderDaysBefore, d.CompletedAt, d.CreatedAt);
}
