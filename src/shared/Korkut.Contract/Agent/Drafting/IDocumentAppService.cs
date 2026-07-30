using Korkut.Contract.Export;

namespace Korkut.Contract.Agent.Drafting;

/// <summary>AI-drafted-document use-cases a controller forwards to.</summary>
public interface IDocumentAppService
{
    /// <summary>Every drafted document in the office, most-recently-updated first.</summary>
    Task<IReadOnlyList<DocBriefResponse>> ListAsync(CancellationToken ct = default);

    Task<DocumentDataResponse> ForSessionAsync(Guid sessionId, CancellationToken ct = default);

    Task<PendingEditResponse> DraftAsync(Guid docId, string instruction, string? petitionType, CancellationToken ct = default);

    Task<DocumentDataResponse> AcceptAsync(Guid docId, CancellationToken ct = default);

    Task<DocumentDataResponse> RevertAsync(Guid docId, CancellationToken ct = default);

    Task<DocumentDataResponse> SaveAsync(Guid docId, string title, string contentMd, CancellationToken ct = default);

    Task<ExportedDocument> ExportAsync(Guid docId, string? format, CancellationToken ct = default);
}
