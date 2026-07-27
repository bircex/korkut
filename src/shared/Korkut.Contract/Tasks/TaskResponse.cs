namespace Korkut.Contract.Tasks;

/// <summary>Transport shape for an office task.</summary>
public sealed record TaskResponse(
    Guid Id, string Title, string Status, Guid? MatterId, Guid? AssigneeId,
    DateTimeOffset? DueAt, DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt);
