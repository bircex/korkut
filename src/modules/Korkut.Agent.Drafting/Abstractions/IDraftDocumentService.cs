using Korkut.Agent.Drafting.Entities;

namespace Korkut.Agent.Drafting.Abstractions;

/// <summary>AI-drafted-document use-cases (stage/accept/revert/save + versions). Clock-driven timestamps.</summary>
public interface IDraftDocumentService
{
    Task<GeneratedDocument> GetOrCreateForSessionAsync(Guid officeId, Guid conversationId, string title, CancellationToken ct = default);

    Task<GeneratedDocument?> GetAsync(Guid officeId, Guid docId, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentVersion>> VersionsAsync(Guid officeId, Guid docId, CancellationToken ct = default);

    Task<Guid?> StagePendingAsync(Guid officeId, Guid docId, string contentMd, string summary, CancellationToken ct = default);

    Task<bool> AcceptPendingAsync(Guid officeId, Guid docId, string? author, CancellationToken ct = default);

    Task<bool> RevertPendingAsync(Guid officeId, Guid docId, CancellationToken ct = default);

    Task<bool> SaveAsync(Guid officeId, Guid docId, string title, string contentMd, string? author, CancellationToken ct = default);
}
