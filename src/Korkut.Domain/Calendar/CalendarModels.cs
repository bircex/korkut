namespace Korkut.Domain.Calendar;

/// <summary>
/// A unified calendar entry (domain read model): an editable <c>calendar_events</c> row,
/// or a read-only hearing/deadline surfaced into the same view. <see cref="Editable"/>
/// distinguishes the two — only editable entries can be patched/deleted.
/// </summary>
public sealed record CalendarEntry(
    Guid Id, string Category, string? Type, string Title,
    DateTimeOffset StartAt, DateTimeOffset? EndAt, bool AllDay,
    Guid? MatterId, string? MatterTitle, Guid? ClientId, string? ClientName,
    Guid? AssigneeId, string? AssigneeName, string? Location, string Status,
    string? Result, string? Notes, string? Rrule, int? RemindBeforeMin, string ReminderStatus,
    bool Editable);

/// <summary>An upcoming-agenda item (hearing or deadline) within the look-ahead window.</summary>
public sealed record AgendaItem(string Kind, DateTimeOffset Date, Guid Id, Guid? MatterId, string Title, string? Status);

/// <summary>Fields for creating an editable calendar event (id/createdAt assigned on persist).</summary>
public sealed record CalendarEventDraft(
    string Category, string Title, DateTimeOffset StartAt, DateTimeOffset? EndAt, bool AllDay,
    string? Type, Guid? MatterId, Guid? ClientId, Guid? AssigneeId, string? Location,
    string? Notes, string? Rrule, int? RemindBeforeMin);

/// <summary>Partial update of an editable calendar event — null fields are left unchanged.</summary>
public sealed record CalendarEventPatch(
    string? Title, string? Type, DateTimeOffset? StartAt, DateTimeOffset? EndAt, bool? AllDay,
    Guid? MatterId, Guid? ClientId, Guid? AssigneeId, string? Location, string? Status,
    string? Result, string? Notes, string? Rrule, int? RemindBeforeMin);
