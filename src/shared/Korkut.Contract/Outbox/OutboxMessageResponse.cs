namespace Korkut.Contract.Outbox;

/// <summary>Transport shape for a queued/draft outbound message.</summary>
public sealed record OutboxMessageResponse(
    Guid Id, string Kind, string To, string Subject, string Body, DateTimeOffset? SendAt,
    string Status, string? Error, DateTimeOffset CreatedAt, DateTimeOffset? SentAt);
