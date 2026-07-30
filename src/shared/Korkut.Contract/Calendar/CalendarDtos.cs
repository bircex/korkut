namespace Korkut.Contract.Calendar;

/// <summary>
/// A unified calendar entry: an editable calendar event, or a hearing/deadline surfaced read-only
/// into the same view. <see cref="Editable"/> distinguishes the two.
/// </summary>
public sealed record CalendarEntryResponse(
    Guid Id, string Category, string? Type, string Title, DateTimeOffset StartAt, DateTimeOffset? EndAt, bool AllDay,
    Guid? MatterId, string? MatterTitle, Guid? ClientId, string? ClientName, Guid? AssigneeId, string? AssigneeName,
    string? Location, string Status, string? Result, string? Notes, string? Rrule, int? RemindBeforeMin,
    string ReminderStatus, bool Editable)
{
    /// <summary>Whether this entry repeats (has a recurrence rule). Derived from <see cref="Rrule"/>.</summary>
    public bool Recurring => !string.IsNullOrEmpty(Rrule);
}

/// <summary>An upcoming-agenda item (hearing or deadline) within the look-ahead window.
/// <see cref="Type"/>/<see cref="Time"/>/<see cref="Location"/> are populated for hearings.</summary>
public sealed record AgendaItemResponse(
    string Kind, DateTimeOffset Date, Guid Id, Guid? MatterId, string Title, string? Status,
    string? Type = null, TimeOnly? Time = null, string? Location = null);

/// <summary>The agenda plus the look-ahead window it was computed for.</summary>
public sealed record AgendaResponse(IReadOnlyList<AgendaItemResponse> Items, int Days);
