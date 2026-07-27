namespace Korkut.Contract.Hearings;

/// <summary>Transport shape for a hearing.</summary>
public sealed record HearingResponse(
    Guid Id, DateTimeOffset ScheduledAt, string? Type, string? Location, string? Result,
    string? Notes, string ReminderStatus);
