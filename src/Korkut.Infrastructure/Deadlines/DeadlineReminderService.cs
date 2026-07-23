using Korkut.Domain.Jobs;
using Korkut.Application.Jobs;
using Korkut.Domain.Common;
using Korkut.Domain.Deadlines;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Deadlines;

/// <summary>
/// Deadline reminders with explicit time-zone / business-date semantics. A due
/// business date is turned into a concrete instant at LOCAL midnight of
/// (due − reminderDaysBefore) in the deadline's IANA zone, so a reminder fires on
/// the office's calendar day regardless of the server clock. Enqueue is idempotent
/// (one outbox row per deadline), matching the outbox/inbox guarantees (KDM-024).
/// </summary>
public sealed class DeadlineReminderService(KorkutDbContext db, IClock clock)
{
    /// <summary>The UTC instant at local midnight of the reminder date in the deadline's zone.</summary>
    public static DateTimeOffset ReminderInstant(Deadline deadline)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(deadline.TimeZoneId);
        var remindDate = deadline.DueDate.AddDays(-deadline.ReminderDaysBefore);
        var localMidnight = new DateTime(remindDate.Year, remindDate.Month, remindDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var offset = tz.GetUtcOffset(localMidnight);
        return new DateTimeOffset(localMidnight, offset).ToUniversalTime();
    }

    public static string ReminderKey(Guid deadlineId) => $"deadline-reminder:{deadlineId}";

    /// <summary>
    /// Enqueue the reminder once. A second call is a no-op (idempotent) — the same
    /// deadline never produces duplicate reminders.
    /// </summary>
    public async Task EnsureReminderAsync(Guid officeId, Deadline deadline, CancellationToken ct = default)
    {
        var key = ReminderKey(deadline.Id);
        var exists = await db.Outbox.AsNoTracking()
            .AnyAsync(x => x.OfficeId == officeId && x.IdempotencyKey == key, ct);
        if (exists)
        {
            return;
        }

        db.Outbox.Add(new OutboxMessage(
            officeId, "deadline.reminder", $"{{\"deadline_id\":\"{deadline.Id}\"}}", key,
            ReminderInstant(deadline), clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }
}
