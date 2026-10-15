using Korkut.Contract.Documents;
using Korkut.Contract.Matters;
using Korkut.Core;
using Korkut.Contract.Documents.Requests;
using Korkut.Contract.Documents.Responses;
using Korkut.Contract.Documents.Storage;
using Korkut.Contract.Upload.SafeUpload;
using Korkut.Documents.Repositories;
using Korkut.Documents.Entities;
using Microsoft.Extensions.Logging;

namespace Korkut.Documents.Services;

/// <summary>
/// The document library over the repository and the upload/lifecycle ports. The two rules
/// that matter are the aggregate's, not this service's: a document is downloadable only
/// once it is clean, and a held document cannot be deleted. Who sees a document: whoever uploaded
/// it, the team of any matter it is linked to (directly or through one of the matter's parties),
/// and whoever sees every matter. Any other document reads as not found.
/// </summary>
public sealed partial class DocumentFileService(
    IDocumentRepository repository,
    DocumentAccess documentAccess,
    IMatterAccess access,
    IMatterLookup matters,
    ISafeUploadService upload,
    IObjectStoreService objectStore,
    IDocumentRelationService relations,
    IDocumentLifecycleService lifecycle,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ExtractionService extraction,
    ILogger<DocumentFileService> logger) : IDocumentFileService
{
    /// <summary>How long a download URL stays valid — long enough to click, short enough to leak nothing.</summary>
    private TimeSpan DownloadUrlLifetime { get; init; } = TimeSpan.FromMinutes(2);

    public Task<IReadOnlyList<DocumentResponse>> ListAsync(Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var (visible, matterOf) = await VisibilityAsync(office, ct);
            var documents = (await repository.ListAsync(office, null, ct)).Where(visible);
            if (matterId is { } id)
            {
                documents = documents.Where(d => d.MatterId == id || matterOf(d).Contains(id));
            }

            return (IReadOnlyList<DocumentResponse>)documents.Select(ToResponse).ToList();
        }, ct);

    public async Task<DocumentResponse> UploadAsync(Stream content, string filename, CancellationToken ct = default)
    {
        var (response, clean) = await InScopeAsync(async office =>
        {
            var result = await upload.IngestAsync(office, currentUser.UserId, content, filename, ct);
            if (result.DocumentId is not { } id)
            {
                // Refused by the validator (bad magic bytes or filename) — nothing was stored.
                throw new UnprocessableException($"Yükleme reddedildi: {result.ValidatorRejection}.");
            }

            var document = await repository.FindActiveAsync(office, id, ct) ?? throw NotFound();
            return (ToResponse(document), result.Status == SafeUploadStatus.Clean);
        }, ct);

        if (clean)
        {
            await ExtractAsync(response.Id, ct);
        }

        return response;
    }

    /// <summary>
    /// Reads a freshly clean document's text, which queues it for the search index. It runs here,
    /// in the uploader's request, because a scanned file goes to OCR and OCR is only ever done for
    /// a caller allowed to read the document; the indexing that follows needs no caller. It runs
    /// after the upload has committed and is best-effort: a failure leaves a stored, downloadable
    /// document that is simply not searchable yet.
    /// </summary>
    private async Task ExtractAsync(Guid documentId, CancellationToken ct)
    {
        try
        {
            await InScopeAsync(office => extraction.ExtractAsync(office, documentId, ct), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ExtractionFailed(logger, documentId, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Text extraction failed for document {DocumentId}; it stays unsearchable.")]
    private static partial void ExtractionFailed(ILogger logger, Guid documentId, Exception ex);

    public Task<PresignResponse> PresignDownloadAsync(Guid documentId, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var document = await VisibleAsync(office, documentId, ct);
            if (!document.IsDownloadable)
            {
                throw new ConflictException("Belge henüz indirilemez (tarama tamamlanmadı veya reddedildi).");
            }

            return new PresignResponse(
                await objectStore.PresignGetAsync(document.Bucket, document.ObjectKey, DownloadUrlLifetime, ct));
        }, ct);

    public Task<DocumentLinkResponse> LinkAsync(Guid documentId, LinkDocumentRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            await VisibleAsync(office, documentId, ct);
            await RequireVisibleTargetAsync(office, request, ct);
            return new DocumentLinkResponse(await relations.LinkAsync(
                office, currentUser.UserId, documentId, request.TargetType!, request.TargetId, request.IsPrimary, ct));
        }, ct);

    public Task UnlinkAsync(Guid documentId, LinkDocumentRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            await VisibleAsync(office, documentId, ct);
            await relations.UnlinkAsync(office, currentUser.UserId, documentId, request.TargetType!, request.TargetId, ct);
            return true;
        }, ct);

    public Task DeleteAsync(Guid documentId, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            await VisibleAsync(office, documentId, ct);
            await lifecycle.TombstoneAsync(office, documentId, ct);
            return true;
        }, ct);

    /// <summary>The document, if it exists and the caller may see it; not found otherwise.</summary>
    private async Task<Document> VisibleAsync(Guid office, Guid documentId, CancellationToken ct)
    {
        var document = await repository.FindActiveAsync(office, documentId, ct) ?? throw NotFound();
        var (visible, _) = await VisibilityAsync(office, ct);
        return visible(document) ? document : throw NotFound();
    }

    /// <summary>A document may only be linked to a matter (or a matter's party) the caller sees.</summary>
    private async Task RequireVisibleTargetAsync(Guid office, LinkDocumentRequest request, CancellationToken ct)
    {
        var scope = await access.ScopeAsync(office, ct);
        var matter = request.TargetType switch
        {
            DocumentRelationTarget.Matter => request.TargetId,
            DocumentRelationTarget.Party => (await matters.PartyMattersAsync(office, ct)).TryGetValue(request.TargetId, out var m) ? m : (Guid?)null,
            _ => null,
        };
        if (matter is { } id && !scope.CanSee(id))
        {
            throw new NotFoundException("Dosya bulunamadı.");
        }
    }

    private Task<(Func<Document, bool> Visible, Func<Document, IReadOnlySet<Guid>> MattersOf)> VisibilityAsync(
        Guid office, CancellationToken ct) => documentAccess.VisibilityAsync(office, ct);

    private static NotFoundException NotFound() => new("Belge bulunamadı.");

    private static DocumentResponse ToResponse(Document d) =>
        new(d.Id, d.OriginalFilename, d.ContentType, d.Size, d.Status, d.Version, d.CreatedAt);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
