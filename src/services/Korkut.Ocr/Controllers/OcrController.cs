using Korkut.Core;
using Korkut.Contract.Documents.OCR;
using Korkut.Security;
using Korkut.Web.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Ocr.Controllers;

/// <summary>The text extracted from an uploaded image or text-less PDF, and how many pages it spanned.</summary>
public sealed record OcrResponse(string Text, int Pages);

/// <summary>
/// The internal OCR surface: turn a scanned image or a text-less PDF into text. Authenticated with
/// the shared access token (matters.read — OCR is a read over a document) and not publicly routed;
/// the gateway keeps it in-cluster and Knowledge ingestion calls it only when a document has no
/// embedded text. When no engine is configured the engine raises OcrUnavailableException, which the
/// shared handler maps to 503.
/// </summary>
[ApiController]
public sealed class OcrController(IOcrService service) : ControllerBase
{
    [HttpPost("/extract")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Extract(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw ApiException.BadRequest("Dosya gerekli.");
        }

        await using var stream = file.OpenReadStream();
        var result = await service.ExtractAsync(stream, file.ContentType ?? "application/octet-stream", ct);
        return Ok(new OcrResponse(result.Text, result.Pages));
    }
}
