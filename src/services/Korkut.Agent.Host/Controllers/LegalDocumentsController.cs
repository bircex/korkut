using Korkut.Contract.Agent.Legal;
using Korkut.Core;
using Korkut.Web.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Agent.Host.Controllers;

/// <summary>
/// Full-text lookup of public legal documents for the UI's İlgili Kararlar drawer. A thin
/// transport over <see cref="ILegalDocumentAppService"/>; the source token and id come from a
/// chat turn's SourceRef. Public-database content only — no tenant data — gated by documents.read
/// like the fetch tool it mirrors.
/// </summary>
[ApiController]
[Route("api/legal/documents")]
public sealed class LegalDocumentsController(ILegalDocumentAppService legal) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.DocumentsRead)]
    public async Task<IActionResult> Get(
        [FromQuery] string? source, [FromQuery] string? id, [FromQuery] int? page, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(id))
        {
            throw ApiException.BadRequest("'source' ve 'id' gereklidir.");
        }

        return Ok(await legal.GetAsync(source.Trim(), id, page, ct));
    }
}
