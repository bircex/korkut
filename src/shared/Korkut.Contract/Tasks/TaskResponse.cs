namespace Korkut.Contract.Tasks;

/// <summary>Transport shape for an office task.</summary>
public sealed record TaskResponse(
    Guid Id, string Title, string? Description, string Status, Guid? MatterId, Guid? AssigneeId, string? AssigneeName,
    DateTimeOffset? DueDate, DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt);
