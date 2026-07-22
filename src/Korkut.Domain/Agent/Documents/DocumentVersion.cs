namespace Korkut.Domain.Agent.Documents;

/// <summary>An immutable snapshot of a document's content at accept/save time.</summary>
public sealed record DocumentVersion(
    Guid Id, Guid DocumentId, string ContentMd, string? Author, string? Note, DateTimeOffset CreatedAt);
