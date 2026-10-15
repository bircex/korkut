using Korkut.Core;

namespace Korkut.Deadlines.Entities;

/// <summary>
/// A legal or internal deadline (süre) on the office calendar. Dates are calendar days in
/// <see cref="TimeZoneId"/>, so a reminder fires on the right local day.
/// </summary>
public sealed class Deadline : Entity, ITenantOwned, IAggregateRoot
{
    public Deadline(
        Guid officeId, string title, DateOnly dueDate, string kind, Guid? matterId,
        string timeZoneId, int reminderDaysBefore, string? notes, DateTimeOffset createdAt, Guid? createdBy = null)
        : base(NewId())
    {
        CreatedBy = createdBy;
        OfficeId = officeId;
        Title = title;
        DueDate = dueDate;
        Kind = kind;
        MatterId = matterId;
        TimeZoneId = timeZoneId;
        ReminderDaysBefore = reminderDaysBefore;
        Notes = notes;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Deadline()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid? MatterId { get; private set; }

    /// <summary>"statutory" | "internal".</summary>
    public string Kind { get; private set; } = "internal";

    public string Title { get; private set; } = string.Empty;

    public DateOnly DueDate { get; private set; }

    public string TimeZoneId { get; private set; } = "Europe/Istanbul";

    public int ReminderDaysBefore { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who added it; a deadline with no matter is visible only to them (and to who sees every matter).</summary>
    public Guid? CreatedBy { get; private set; }

    public bool IsCompleted => CompletedAt is not null;

    /// <summary>Completes the deadline once; a second attempt is a no-op that reports false.</summary>
    public bool Complete(DateTimeOffset completedAt)
    {
        if (IsCompleted)
        {
            return false;
        }

        CompletedAt = completedAt;
        return true;
    }
}
