using Korkut.Jobs.Entities;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Jobs.Persistence;

/// <summary>
/// Schedules a deadline reminder onto the transactional outbox with explicit time-zone /
/// business-date semantics. A due business date is turned into a concrete instant at LOCAL
/// midnight of (due − reminderDaysBefore) in the deadline's IANA zone, so a reminder fires on
/// the office's calendar day regardless of the server clock. Enqueue is idempotent (one outbox
/// row per deadline). Takes primitives (not the Deadline entity) so Jobs never references the
/// Deadlines module.
/// </summary>
public sealed class DeadlineReminderService(DbContext db, IClock clock)
{
    /// <summary>The UTC instant at local midnight of the reminder date in the deadline's zone.</summary>
    public static DateTimeOffset ReminderInstant(DateOnly dueDate, string timeZoneId, int reminderDaysBefore)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var remindDate = dueDate.AddDays(-reminderDaysBefore);
        var localMidnight = new DateTime(remindDate.Year, remindDate.Month, remindDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var offset = tz.GetUtcOffset(localMidnight);
        return new DateTimeOffset(localMidnight, offset).ToUniversalTime();
    }

    public static string ReminderKey(Guid deadlineId) => $"deadline-reminder:{deadlineId}";

    /// <summary>
    /// Enqueue the reminder once. A second call is a no-op (idempotent) — the same deadline
    /// never produces duplicate reminders.
    /// </summary>
    public async Task EnsureReminderAsync(
        Guid officeId, Guid deadlineId, DateOnly dueDate, string timeZoneId, int reminderDaysBefore, CancellationToken ct = default)
    {
        var key = ReminderKey(deadlineId);
        var exists = await db.Set<OutboxMessage>().AsNoTracking()
            .AnyAsync(x => x.OfficeId == officeId && x.IdempotencyKey == key, ct);
        if (exists)
        {
            return;
        }

        db.Set<OutboxMessage>().Add(new OutboxMessage(
            officeId, "deadline.reminder", $"{{\"deadline_id\":\"{deadlineId}\"}}", key,
            ReminderInstant(dueDate, timeZoneId, reminderDaysBefore), clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }
}
