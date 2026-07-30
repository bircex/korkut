namespace Korkut.Contract.Hearings;

/// <summary>Transport shape for a hearing. <see cref="ScheduledAt"/> is the canonical instant;
/// <see cref="Date"/> + <see cref="Time"/> are the same value split for the UI's date/time inputs.</summary>
public sealed record HearingResponse(
    Guid Id, Guid MatterId, DateTimeOffset ScheduledAt, DateOnly Date, TimeOnly Time,
    string? Type, string? Location, string? Result, string? Notes, string ReminderStatus);
