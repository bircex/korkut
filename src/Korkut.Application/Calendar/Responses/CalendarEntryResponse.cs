namespace Korkut.Application.Calendar.Responses;

/// <summary>
/// A unified calendar entry: an editable calendar event, or a hearing/deadline surfaced
/// read-only into the same view. <see cref="Editable"/> distinguishes the two.
/// </summary>
public sealed record CalendarEntryResponse(
    Guid Id, string Category, string? Type, string Title, DateTimeOffset StartAt, DateTimeOffset? EndAt, bool AllDay,
    Guid? MatterId, string? MatterTitle, Guid? ClientId, string? ClientName, Guid? AssigneeId, string? AssigneeName,
    string? Location, string Status, string? Result, string? Notes, string? Rrule, int? RemindBeforeMin,
    string ReminderStatus, bool Editable);
