using Korkut.Application.Documents.Requests;
using Korkut.Application.Documents.Responses;

namespace Korkut.Application.Documents;

/// <summary>
/// The office's document library: what has been uploaded, getting a file back out, filing
/// documents against the record they belong to, and deleting them. Uploading runs the
/// quarantine pipeline (magic-byte validation, then a malware scan) and downloading is
/// refused until that pipeline has proven the bytes clean.
/// </summary>
public interface IDocumentFileService
{
    Task<IReadOnlyList<DocumentResponse>> ListAsync(Guid? matterId, CancellationToken ct = default);

    /// <summary>
    /// Ingests an uploaded file. A file the validator refuses is never stored, and the
    /// caller is told why.
    /// </summary>
    Task<DocumentResponse> UploadAsync(Stream content, string filename, CancellationToken ct = default);

    /// <summary>A short-lived download URL, refused while the document is not clean.</summary>
    Task<PresignResponse> PresignDownloadAsync(Guid documentId, CancellationToken ct = default);

    Task<DocumentLinkResponse> LinkAsync(Guid documentId, LinkDocumentRequest request, CancellationToken ct = default);

    Task UnlinkAsync(Guid documentId, LinkDocumentRequest request, CancellationToken ct = default);

    /// <summary>Deletes (tombstones) the document; refused while it is under legal hold.</summary>
    Task DeleteAsync(Guid documentId, CancellationToken ct = default);
}
