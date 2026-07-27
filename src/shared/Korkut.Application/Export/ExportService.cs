using Korkut.Application.Errors;
using Korkut.Application.Export.Requests;

namespace Korkut.Application.Export;

/// <summary>Export over the renderer port; an unsupported format is refused here, not deep in the renderer.</summary>
public sealed class ExportService(IDocumentExporter exporter) : IExportService
{
    public ExportedDocument Export(ExportDocumentRequest request)
        => ExportFormat.IsSupported(request.Format)
            ? exporter.Export(request.Format!, request.Title!, request.Markdown!)
            : throw new BadRequestException("format 'docx' veya 'udf' olmalıdır.");
}
