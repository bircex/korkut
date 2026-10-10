using Korkut.Core;
using Korkut.Contract.Documents;
using Korkut.Contract.Documents.Requests;
using Korkut.Documents.Storage;
using Korkut.Security;
using Korkut.Web.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>
/// The office's document library: upload through the quarantine pipeline, short-lived
/// download URLs that only exist once a file is proven clean, filing documents against a
/// client/matter/party, and legal-hold-aware deletion. Reading needs documents.read,
/// everything that changes something needs documents.write.
/// </summary>
[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(IDocumentFileService documents, IServiceProvider services) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.DocumentsRead)]
    public async Task<IActionResult> List([FromQuery] Guid? matterId, CancellationToken ct)
        => Ok(await documents.ListAsync(matterId, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.DocumentsWrite)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw ApiException.BadRequest("Dosya gerekli.");
        }

        await using var stream = file.OpenReadStream();
        var created = await documents.UploadAsync(stream, file.FileName, ct);
        return Created($"/api/documents/{created.Id}", created);
    }

    [HttpGet("{id:guid}/download")]
    [Authorize(Policy = Permissions.DocumentsRead)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
        => Ok(await documents.PresignDownloadAsync(id, ct));

    /// <summary>
    /// Serves one object named by a signed, short-lived link from <see cref="Download"/> when the
    /// documents live on the filesystem: the token itself is the permission, exactly like an S3
    /// presigned URL, which is why there is no session check here. It is served inline (the app
    /// opens it for preview) and never cached, so a link that outlives its token reopens nothing.
    /// </summary>
    [HttpGet("files/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> ServeFile(string token, CancellationToken ct)
    {
        var store = services.GetService<FileSystemObjectStoreService>();
        var signer = services.GetService<ObjectUrlSigner>();
        if (store is null || signer is null || signer.Verify(token) is not { } target
            || !await store.ExistsAsync(target.Bucket, target.Key, ct))
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        var stream = await store.GetAsync(target.Bucket, target.Key, ct);
        return File(stream, store.ContentTypeOf(target.Bucket, target.Key));
    }

    [HttpPost("{id:guid}/links")]
    [Authorize(Policy = Permissions.DocumentsWrite)]
    public async Task<IActionResult> Link(Guid id, [FromBody] LinkDocumentRequest body, CancellationToken ct)
        => Ok(await documents.LinkAsync(id, body, ct));

    [HttpDelete("{id:guid}/links")]
    [Authorize(Policy = Permissions.DocumentsWrite)]
    public async Task<IActionResult> Unlink(Guid id, [FromBody] LinkDocumentRequest body, CancellationToken ct)
    {
        await documents.UnlinkAsync(id, body, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.DocumentsWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await documents.DeleteAsync(id, ct);
        return NoContent();
    }
}
