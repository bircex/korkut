using Korkut.Contract.Export;
using Korkut.Application.Export.Requests;

namespace Korkut.Application.Export;

/// <summary>
/// Turns markdown the caller already has into a file they can download. Nothing is read
/// from or written to the office's data, so there is no tenant scope here.
/// </summary>
public interface IExportService
{
    ExportedDocument Export(ExportDocumentRequest request);
}
