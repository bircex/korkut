using Korkut.Core;

namespace Korkut.Agent.Drafting.Entities;

/// <summary>
/// An AI-drafted document tied to a conversation (one per session). A staged edit lives in the
/// <c>Pending*</c> fields until a human accepts it — which promotes it to the current content and
/// snapshots a new version — or reverts it. Distinct from an uploaded
/// <see cref="Korkut.Documents.Document"/>: this is generated content, not a stored file.
/// </summary>
public sealed class GeneratedDocument : Entity, ITenantOwned, IAggregateRoot
{
    public const string Draft = "draft";
    
    public const string Final = "final";

    public GeneratedDocument(Guid officeId, Guid conversationId, string title, DateTimeOffset now)
        : base(NewId())
    {
        OfficeId = officeId;
        ConversationId = conversationId;
        Title = title;
        Status = Draft;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private GeneratedDocument()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>The owning conversation (unique — one generated document per session).</summary>
    public Guid ConversationId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string ContentMd { get; private set; } = string.Empty;

    /// <summary>"draft" | "final".</summary>
    public string Status { get; private set; } = Draft;

    // --- Pending edit (proposed but not yet accepted). Null when there is none. ---
    public Guid? PendingId { get; private set; }

    public string? PendingContentMd { get; private set; }

    public string? PendingSummary { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Whether a proposed edit is waiting for a human to accept or revert it.</summary>
    public bool HasPending => PendingContentMd is not null;

    /// <summary>
    /// Promotes the pending edit to the current content and clears it. False when there was no
    /// pending edit, so the caller does not snapshot a version for a no-op.
    /// </summary>
    public bool AcceptPending(DateTimeOffset now)
    {
        if (!HasPending)
        {
            return false;
        }

        ContentMd = PendingContentMd!;
        PendingId = null;
        PendingContentMd = null;
        PendingSummary = null;
        UpdatedAt = now;
        return true;
    }

    /// <summary>A direct human save: replaces the title and content (a version is snapshotted by the caller).</summary>
    public void Save(string title, string contentMd, DateTimeOffset now)
    {
        Title = title;
        ContentMd = contentMd;
        UpdatedAt = now;
    }
}
