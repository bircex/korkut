namespace Korkut.Domain.Outbox;

/// <summary>
/// An outbound message in the human-review queue (domain model): a draft the assistant
/// produced, edited/confirmed/cancelled by a person before it is sent. Only DRAFT messages
/// may be edited or confirmed; confirming moves draft → queued.
/// </summary>
public sealed record OutboundMessage(
    Guid Id, string Kind, string To, string Subject, string Body, DateTimeOffset? SendAt,
    string Status, string? Error, DateTimeOffset CreatedAt, DateTimeOffset? SentAt);
