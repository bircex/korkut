using Korkut.Domain.Common;
namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// A tenant-owned calendar entry (appointment, reminder, or a manually-added hearing/
/// deadline marker). This is the editable calendar store. The unified calendar VIEW
/// (GET /api/calendar/events) also surfaces the office's real hearings and deadlines
/// read-only, but those live in their own tables and are edited through their own
/// domains — only <c>calendar_events</c> rows are mutated here.
/// </summary>
public sealed class CalendarEventEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    /// <summary>"hearing" | "deadline" | "appointment" | "reminder".</summary>
    public string Category { get; set; } = "appointment";

    /// <summary>Free-form sub-type (e.g. "ön inceleme", "tahkikat").</summary>
    public string? Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset? EndAt { get; set; }

    public bool AllDay { get; set; }

    public Guid? MatterId { get; set; }

    public Guid? ClientId { get; set; }

    /// <summary>The office user responsible (optional).</summary>
    public Guid? AssigneeId { get; set; }

    public string? Location { get; set; }

    /// <summary>"scheduled" | "done" | "cancelled".</summary>
    public string Status { get; set; } = "scheduled";

    public string? Result { get; set; }

    public string? Notes { get; set; }

    /// <summary>An iCalendar RRULE string for recurring events (stored; not expanded server-side yet).</summary>
    public string? Rrule { get; set; }

    /// <summary>Minutes before <see cref="StartAt"/> to remind (null = no reminder).</summary>
    public int? RemindBeforeMin { get; set; }

    /// <summary>"pending" | "sent" | "dismissed".</summary>
    public string ReminderStatus { get; set; } = "pending";

    public DateTimeOffset CreatedAt { get; set; }
}
