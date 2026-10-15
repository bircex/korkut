namespace Korkut.Contract.Deadlines;

/// <summary>Transport shape for a deadline. <see cref="Type"/> is a free-text label; <see cref="Status"/>
/// is "done" once completed, else "open".</summary>
public sealed record DeadlineResponse(
    Guid Id, string Title, DateOnly DueDate, string? Type, string Status, Guid? MatterId, string? Notes,
    string TimeZoneId, int ReminderDaysBefore, DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt, Guid? CreatedBy = null);
