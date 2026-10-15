using Korkut.Core;

namespace Korkut.Tasks.Entities;

/// <summary>
/// A unit of office agenda work ("ajanda kaydı"): a to-do, reminder or appointment, optionally
/// attached to a matter/client and an assignee, and optionally scheduled so it surfaces on the
/// calendar. The <see cref="Category"/> discriminates görev / hatırlatma / randevu; hearings and
/// deadlines are NOT stored here — they stay owned by their own domains and only overlay the
/// calendar read-only. Named <c>WorkTask</c> so it never collides with
/// <see cref="System.Threading.Tasks.Task"/>.
/// </summary>
public sealed class WorkTask : Entity, ITenantOwned, IAggregateRoot
{
    public WorkTask(
        Guid officeId, string category, string title, string? description, string? type,
        Guid? matterId, Guid? assigneeId, Guid? clientId,
        DateTimeOffset? dueAt, DateTimeOffset? endAt, bool allDay,
        string? location, string? rrule, int? remindBeforeMin, DateTimeOffset createdAt, Guid? createdBy = null)
        : base(NewId())
    {
        CreatedBy = createdBy;
        OfficeId = officeId;
        Category = category;
        Title = title;
        Description = description;
        Type = type;
        MatterId = matterId;
        AssigneeId = assigneeId;
        ClientId = clientId;
        DueAt = dueAt;
        EndAt = endAt;
        AllDay = allDay;
        Location = location;
        Rrule = rrule;
        RemindBeforeMin = remindBeforeMin;
        Status = "open";
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private WorkTask()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>"gorev" (default) | "hatirlatma" | "randevu".</summary>
    public string Category { get; private set; } = "gorev";

    public Guid? MatterId { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public Guid? ClientId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Optional free-text label (e.g. "ön inceleme", "toplantı"); mirrors the old event type.</summary>
    public string? Type { get; private set; }

    /// <summary>"open" | "in_progress" | "done".</summary>
    public string Status { get; private set; } = "open";

    /// <summary>The scheduled point of the record ("when"). All-day items carry just the date;
    /// timed appointments carry the start time here and the end in <see cref="EndAt"/>.</summary>
    public DateTimeOffset? DueAt { get; private set; }

    public DateTimeOffset? EndAt { get; private set; }

    public bool AllDay { get; private set; } = true;

    public string? Location { get; private set; }

    /// <summary>iCalendar recurrence rule, when the record repeats.</summary>
    public string? Rrule { get; private set; }

    public int? RemindBeforeMin { get; private set; }

    /// <summary>"pending" | "sent" | "dismissed". Stored for future reminder delivery.</summary>
    public string ReminderStatus { get; private set; } = "pending";

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who added it. A record is visible with its matter, or to whoever added it or it is assigned to.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>Moving to "done" stamps the completion time; moving away clears it.</summary>
    public void SetStatus(string status, DateTimeOffset now)
    {
        Status = status;
        CompletedAt = status == "done" ? now : null;
    }

    public void Reassign(Guid? assigneeId) => AssigneeId = assigneeId;

    /// <summary>Applies a partial update; every omitted (null) field keeps its current value.
    /// Status changes also (re)stamp <see cref="CompletedAt"/> via <see cref="SetStatus"/>.</summary>
    public void Update(
        string? category, string? title, string? description, string? type,
        Guid? matterId, Guid? assigneeId, Guid? clientId,
        DateTimeOffset? dueAt, DateTimeOffset? endAt, bool? allDay,
        string? location, string? status, string? rrule, int? remindBeforeMin, DateTimeOffset now)
    {
        Category = category ?? Category;
        if (title is not null) Title = title;
        Description = description ?? Description;
        Type = type ?? Type;
        MatterId = matterId ?? MatterId;
        AssigneeId = assigneeId ?? AssigneeId;
        ClientId = clientId ?? ClientId;
        DueAt = dueAt ?? DueAt;
        EndAt = endAt ?? EndAt;
        AllDay = allDay ?? AllDay;
        Location = location ?? Location;
        Rrule = rrule ?? Rrule;
        RemindBeforeMin = remindBeforeMin ?? RemindBeforeMin;
        if (status is not null) SetStatus(status, now);
    }
}
