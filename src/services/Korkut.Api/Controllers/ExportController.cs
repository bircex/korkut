using Korkut.Core;
using Korkut.Contract.Export;
using Korkut.Contract.Export.Requests;
using Korkut.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>
/// Renders caller-supplied markdown to a downloadable Word (.docx) or UYAP (.udf) file.
/// The content comes from the request, so nothing in the office's data is touched;
/// matters.read is enough.
/// </summary>
[ApiController]
[Route("api/export")]
public sealed class ExportController(IExportService export) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.MattersRead)]
    public IActionResult Export([FromBody] ExportDocumentRequest body)
    {
        var document = export.Export(body);
        return File(document.Bytes, document.ContentType, document.FileName);
    }
}
