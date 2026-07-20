namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// A tenant-owned AI-drafted document tied to a conversation (one per session). Holds the
/// current markdown, an optional PENDING edit (a proposed change awaiting accept/revert),
/// and is versioned via <see cref="DocumentVersionEntity"/> on each accept/save. Distinct
/// from <see cref="DocumentEntity"/> (uploaded files) — this is generated content.
/// </summary>
public sealed class GeneratedDocumentEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    /// <summary>The owning conversation (unique — one generated document per session).</summary>
    public Guid ConversationId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ContentMd { get; set; } = string.Empty;

    /// <summary>"draft" | "final".</summary>
    public string Status { get; set; } = "draft";

    // --- Pending edit (proposed but not yet accepted). Null when there is none. ---
    public Guid? PendingId { get; set; }

    public string? PendingContentMd { get; set; }

    public string? PendingSummary { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>An immutable snapshot of a generated document's content (an accepted/saved version).</summary>
public sealed class DocumentVersionEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid DocumentId { get; set; }

    public string ContentMd { get; set; } = string.Empty;

    /// <summary>Display attribution (the user's email) for who produced this version.</summary>
    public string? Author { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
