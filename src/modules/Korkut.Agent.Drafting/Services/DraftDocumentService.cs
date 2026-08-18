using Korkut.Agent.Drafting.Repositories;
using Korkut.Agent.Drafting.Abstractions;
using Korkut.Agent.Drafting.Entities;
using Korkut.Core;

namespace Korkut.Agent.Drafting.Services;

/// <summary>Draft-document orchestration over the repository port.</summary>
internal sealed class DraftDocumentService(IDraftDocumentRepository repo, IClock clock) : IDraftDocumentService
{
    public Task<GeneratedDocument> GetOrCreateForSessionAsync(Guid officeId, Guid conversationId, string title, CancellationToken ct = default)
        => repo.GetOrCreateForSessionAsync(officeId, conversationId, title, clock.UtcNow, ct);

    public Task<GeneratedDocument?> GetAsync(Guid officeId, Guid docId, CancellationToken ct = default)
        => repo.GetAsync(officeId, docId, ct);

    public Task<IReadOnlyList<GeneratedDocument>> ListAsync(Guid officeId, CancellationToken ct = default)
        => repo.ListAsync(officeId, ct);

    public Task<IReadOnlyList<DocumentVersion>> VersionsAsync(Guid officeId, Guid docId, CancellationToken ct = default)
        => repo.VersionsAsync(officeId, docId, ct);

    public Task<Guid?> StagePendingAsync(Guid officeId, Guid docId, string contentMd, string summary, CancellationToken ct = default)
        => repo.StagePendingAsync(officeId, docId, contentMd, summary, clock.UtcNow, ct);

    public Task<bool> AcceptPendingAsync(Guid officeId, Guid docId, string? author, CancellationToken ct = default)
        => repo.AcceptPendingAsync(officeId, docId, author, clock.UtcNow, ct);

    public Task<bool> RevertPendingAsync(Guid officeId, Guid docId, CancellationToken ct = default)
        => repo.RevertPendingAsync(officeId, docId, clock.UtcNow, ct);

    public Task<bool> SaveAsync(Guid officeId, Guid docId, string title, string contentMd, string? author, CancellationToken ct = default)
        => repo.SaveAsync(officeId, docId, title, contentMd, author, clock.UtcNow, ct);
}
