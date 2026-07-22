using Korkut.Application.Documents.Export;
using Korkut.Application.Agent.Contracts;

namespace Korkut.Application.Agent.Documents;

/// <summary>AI-drafted-document use-cases a controller forwards to.</summary>
public interface IDocumentAppService
{
    Task<DocumentDataResponse> ForSessionAsync(Guid sessionId, CancellationToken ct = default);

    Task<PendingEditResponse> DraftAsync(Guid docId, string instruction, string? petitionType, CancellationToken ct = default);

    Task<DocumentDataResponse> AcceptAsync(Guid docId, CancellationToken ct = default);

    Task<DocumentDataResponse> RevertAsync(Guid docId, CancellationToken ct = default);

    Task<DocumentDataResponse> SaveAsync(Guid docId, string title, string contentMd, CancellationToken ct = default);

    Task<ExportedDocument> ExportAsync(Guid docId, string? format, CancellationToken ct = default);
}
