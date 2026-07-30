using Korkut.Contract.Documents;
using Korkut.Core;
using Korkut.Contract.Documents.Requests;
using Korkut.Contract.Documents.Responses;
using Korkut.Contract.Documents.Storage;
using Korkut.Contract.Upload.SafeUpload;
using Korkut.Documents.Repositories;
using Korkut.Documents.Entities;

namespace Korkut.Documents.Services;

/// <summary>
/// The document library over the repository and the upload/lifecycle ports. The two rules
/// that matter are the aggregate's, not this service's: a document is downloadable only
/// once it is clean, and a held document cannot be deleted.
/// </summary>
public sealed class DocumentFileService(
    IDocumentRepository repository,
    ISafeUploadService upload,
    IObjectStoreService objectStore,
    IDocumentRelationService relations,
    IDocumentLifecycleService lifecycle,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IDocumentFileService
{
    /// <summary>How long a download URL stays valid — long enough to click, short enough to leak nothing.</summary>
    private TimeSpan DownloadUrlLifetime { get; init; } = TimeSpan.FromMinutes(2);

    public Task<IReadOnlyList<DocumentResponse>> ListAsync(Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
            (IReadOnlyList<DocumentResponse>)(await repository.ListAsync(office, matterId, ct)).Select(ToResponse).ToList(), ct);

    public Task<DocumentResponse> UploadAsync(Stream content, string filename, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var result = await upload.IngestAsync(office, currentUser.UserId, content, filename, ct);
            if (result.DocumentId is not { } id)
            {
                // Refused by the validator (bad magic bytes or filename) — nothing was stored.
                throw new UnprocessableException($"Yükleme reddedildi: {result.ValidatorRejection}.");
            }

            var document = await repository.FindActiveAsync(office, id, ct) ?? throw NotFound();
            return ToResponse(document);
        }, ct);

    public Task<PresignResponse> PresignDownloadAsync(Guid documentId, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var document = await repository.FindActiveAsync(office, documentId, ct) ?? throw NotFound();
            if (!document.IsDownloadable)
            {
                throw new ConflictException("Belge henüz indirilemez (tarama tamamlanmadı veya reddedildi).");
            }

            return new PresignResponse(
                await objectStore.PresignGetAsync(document.Bucket, document.ObjectKey, DownloadUrlLifetime, ct));
        }, ct);

    public Task<DocumentLinkResponse> LinkAsync(Guid documentId, LinkDocumentRequest request, CancellationToken ct = default)
        => InScopeAsync(async office => new DocumentLinkResponse(await relations.LinkAsync(
            office, currentUser.UserId, documentId, request.TargetType!, request.TargetId, request.IsPrimary, ct)), ct);

    public Task UnlinkAsync(Guid documentId, LinkDocumentRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            await relations.UnlinkAsync(office, currentUser.UserId, documentId, request.TargetType!, request.TargetId, ct);
            return true;
        }, ct);

    public Task DeleteAsync(Guid documentId, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            await lifecycle.TombstoneAsync(office, documentId, ct);
            return true;
        }, ct);

    private static NotFoundException NotFound() => new("Belge bulunamadı.");

    private static DocumentResponse ToResponse(Document d) =>
        new(d.Id, d.OriginalFilename, d.ContentType, d.Size, d.Status, d.Version, d.CreatedAt);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
