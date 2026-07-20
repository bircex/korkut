using System.ComponentModel.DataAnnotations;
using Korkut.Api.Errors;
using Korkut.Application.Security;
using Korkut.Infrastructure.Documents;

namespace Korkut.Api.Endpoints;

public sealed record ExportRequest
{
    [Required, RegularExpression("docx|udf", ErrorMessage = "format 'docx' veya 'udf' olmalıdır.")]
    public string? Format { get; init; }

    [Required, MaxLength(300)]
    public string? Title { get; init; }

    [Required]
    public string? Markdown { get; init; }
}

/// <summary>
/// Document export: renders supplied markdown to a downloadable Word (.docx) or UYAP
/// (.udf) file. Authenticated (matters.read); the content is provided by the caller, so
/// no tenant DB access is needed. Returns the binary file with a download filename.
/// </summary>
public static class ExportEndpoints
{
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/export", (ExportRequest body) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var doc = MarkdownDocumentExporter.Export(body.Format!, body.Title!, body.Markdown!);
                return Results.File(doc.Bytes, doc.ContentType, doc.FileName);
            })
            .RequireAuthorization(Permissions.MattersRead);

        return app;
    }
}
