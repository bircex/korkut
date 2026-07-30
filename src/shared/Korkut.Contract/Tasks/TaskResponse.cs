namespace Korkut.Contract.Tasks;

/// <summary>Transport shape for an office agenda record (görev / hatırlatma / randevu).</summary>
public sealed record TaskResponse(
    Guid Id, string Category, string Title, string? Description, string? Type, string Status,
    Guid? MatterId, Guid? AssigneeId, string? AssigneeName, Guid? ClientId,
    DateTimeOffset? DueDate, DateTimeOffset? EndAt, bool AllDay, string? Location,
    string? Rrule, int? RemindBeforeMin, string ReminderStatus,
    DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt);
