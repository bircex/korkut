using Korkut.Application.Documents;
using Korkut.Application.Documents.OCR;
using Korkut.Application.Security;
using Korkut.Web.Errors;

namespace Korkut.Ocr.Endpoints;

public sealed record OcrResponse(string Text, int Pages);

/// <summary>
/// The internal OCR surface: turn a scanned image / text-less PDF into text. Authenticated with the
/// shared access token (requires matters.read — OCR is a read over a document). Not publicly routed
/// (the gateway keeps it in-cluster); called on demand by Knowledge ingestion only when a document
/// has no embedded text. Returns 503 when no OCR engine is configured (the in-code scaffold).
/// </summary>
public static class OcrEndpoints
{
    public static IEndpointRouteBuilder MapOcrEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/extract", async (HttpContext http, IOcrEngine engine, CancellationToken ct) =>
            {
                if (!http.Request.HasFormContentType)
                {
                    throw ApiException.BadRequest("multipart/form-data bekleniyor.");
                }

                var form = await http.Request.ReadFormAsync(ct);
                var file = form.Files["file"];
                if (file is null || file.Length == 0)
                {
                    throw ApiException.BadRequest("Dosya gerekli.");
                }

                try
                {
                    await using var stream = file.OpenReadStream();
                    var result = await engine.ExtractAsync(stream, file.ContentType ?? "application/octet-stream", ct);
                    return Results.Ok(new OcrResponse(result.Text, result.Pages));
                }
                catch (OcrUnavailableException ex)
                {
                    return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .RequireAuthorization(Permissions.MattersRead)
            .DisableAntiforgery();

        return app;
    }
}
