using Korkut.Agent.Drafting;
using Korkut.Agent.Drafting.Entities;
namespace Korkut.Agent.Drafting;

/// <summary>
/// Persistence port for AI-drafted documents tied to a conversation: get-or-create per
/// session, stage/accept/revert a pending edit, direct save, and version history. Accepting
/// a pending edit (or a save) snapshots an immutable version. Tenant context is set by the
/// caller (RLS); timestamps come from the service.
/// </summary>
public interface IDraftDocumentRepository
{
    Task<GeneratedDocument> GetOrCreateForSessionAsync(Guid officeId, Guid conversationId, string title, DateTimeOffset now, CancellationToken ct = default);

    Task<GeneratedDocument?> GetAsync(Guid officeId, Guid docId, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentVersion>> VersionsAsync(Guid officeId, Guid docId, CancellationToken ct = default);

    /// <summary>Stage a pending edit; returns the pending id, or null if the document is missing.</summary>
    Task<Guid?> StagePendingAsync(Guid officeId, Guid docId, string contentMd, string summary, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Apply the pending edit → current content + a new version, then clear it. False if missing/none.</summary>
    Task<bool> AcceptPendingAsync(Guid officeId, Guid docId, string? author, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Discard the pending edit. False if the document is missing.</summary>
    Task<bool> RevertPendingAsync(Guid officeId, Guid docId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Directly save title + content and snapshot a version. False if missing.</summary>
    Task<bool> SaveAsync(Guid officeId, Guid docId, string title, string contentMd, string? author, DateTimeOffset now, CancellationToken ct = default);
}
