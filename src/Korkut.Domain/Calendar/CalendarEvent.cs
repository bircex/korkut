using Korkut.Domain.Common;

namespace Korkut.Domain.Calendar;

/// <summary>
/// An editable calendar entry. Hearings and deadlines also appear on the calendar, but they
/// are owned by their own domains and surface into the unified view read-only — only rows of
/// this aggregate can be created, patched or deleted.
/// </summary>
public sealed class CalendarEvent : Entity, ITenantOwned, IAggregateRoot
{
    public CalendarEvent(
        Guid officeId, string category, string title, DateTimeOffset startAt, DateTimeOffset? endAt, bool allDay,
        string? type, Guid? matterId, Guid? clientId, Guid? assigneeId, string? location, string? notes,
        string? rrule, int? remindBeforeMin, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Category = category;
        Title = title;
        StartAt = startAt;
        EndAt = endAt;
        AllDay = allDay;
        Type = type;
        MatterId = matterId;
        ClientId = clientId;
        AssigneeId = assigneeId;
        Location = location;
        Notes = notes;
        Rrule = rrule;
        RemindBeforeMin = remindBeforeMin;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private CalendarEvent()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>"hearing" | "deadline" | "appointment" | "reminder".</summary>
    public string Category { get; private set; } = "appointment";

    public string? Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public DateTimeOffset StartAt { get; private set; }

    public DateTimeOffset? EndAt { get; private set; }

    public bool AllDay { get; private set; }

    public Guid? MatterId { get; private set; }

    public Guid? ClientId { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public string? Location { get; private set; }

    /// <summary>"scheduled" | "done" | "cancelled".</summary>
    public string Status { get; private set; } = "scheduled";

    public string? Result { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>iCalendar recurrence rule, when the event repeats.</summary>
    public string? Rrule { get; private set; }

    public int? RemindBeforeMin { get; private set; }

    /// <summary>"pending" | "sent" | "dismissed".</summary>
    public string ReminderStatus { get; private set; } = "pending";

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Applies a partial update; every omitted (null) field keeps its current value.</summary>
    public void Update(
        string? title, string? type, DateTimeOffset? startAt, DateTimeOffset? endAt, bool? allDay,
        Guid? matterId, Guid? clientId, Guid? assigneeId, string? location, string? status,
        string? result, string? notes, string? rrule, int? remindBeforeMin)
    {
        Title = title ?? Title;
        Type = type ?? Type;
        StartAt = startAt ?? StartAt;
        EndAt = endAt ?? EndAt;
        AllDay = allDay ?? AllDay;
        MatterId = matterId ?? MatterId;
        ClientId = clientId ?? ClientId;
        AssigneeId = assigneeId ?? AssigneeId;
        Location = location ?? Location;
        Status = status ?? Status;
        Result = result ?? Result;
        Notes = notes ?? Notes;
        Rrule = rrule ?? Rrule;
        RemindBeforeMin = remindBeforeMin ?? RemindBeforeMin;
    }
}
