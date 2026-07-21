namespace Korkut.Domain.Agent;

/// <summary>
/// An AI-drafted document tied to a conversation (domain model). A staged edit lives in the
/// <c>Pending*</c> fields until a human accepts it (→ current content + a new version) or reverts it.
/// </summary>
public sealed record GeneratedDocument(
    Guid Id, Guid OfficeId, Guid ConversationId, string Title, string ContentMd, string Status,
    Guid? PendingId, string? PendingContentMd, string? PendingSummary,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>An immutable snapshot of a document's content at accept/save time.</summary>
public sealed record DocumentVersion(
    Guid Id, Guid DocumentId, string ContentMd, string? Author, string? Note, DateTimeOffset CreatedAt);
