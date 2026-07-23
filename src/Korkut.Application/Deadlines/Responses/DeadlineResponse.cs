namespace Korkut.Application.Deadlines.Responses;

/// <summary>Transport shape for a deadline.</summary>
public sealed record DeadlineResponse(
    Guid Id, string Title, DateOnly DueDate, string Kind, Guid? MatterId, string TimeZoneId,
    int ReminderDaysBefore, DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt);
