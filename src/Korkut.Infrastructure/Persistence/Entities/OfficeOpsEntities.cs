using Korkut.Domain.Common;
namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>A tenant-owned task with an owner and a status lifecycle.</summary>
public sealed class TaskEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid? MatterId { get; set; }

    public Guid? AssigneeId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>"open" | "in_progress" | "done" | "cancelled".</summary>
    public string Status { get; set; } = "open";

    public DateTimeOffset? DueAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A statutory or internal deadline. The due value is a BUSINESS DATE interpreted in
/// an explicit IANA time zone — not a bare instant — so reminders and "is it due"
/// fall on the office's calendar day, independent of the server's clock (KDM-024).
/// </summary>
public sealed class DeadlineEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid? MatterId { get; set; }

    /// <summary>"statutory" | "internal".</summary>
    public string Kind { get; set; } = "internal";

    public string Title { get; set; } = string.Empty;

    /// <summary>The business date (no time-of-day).</summary>
    public DateOnly DueDate { get; set; }

    /// <summary>IANA time zone the business date is interpreted in (e.g. "Europe/Istanbul").</summary>
    public string TimeZoneId { get; set; } = "Europe/Istanbul";

    /// <summary>Days before the due date to remind (0 = on the day).</summary>
    public int ReminderDaysBefore { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>An immutable communication note (call/email/meeting log) on a matter/client.</summary>
public sealed class CommunicationNoteEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid? MatterId { get; set; }

    public Guid? ClientId { get; set; }

    /// <summary>"call" | "email" | "meeting" | "note".</summary>
    public string Channel { get; set; } = "note";

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
