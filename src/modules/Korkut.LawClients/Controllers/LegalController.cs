using Korkut.Contract.LawClients;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.LawClients.Controllers;

/// <summary>
/// Public Turkish legal database access outside the chat tool loop: full-text lookup for the UI's
/// İlgili Kararlar drawer (unchanged route, was <c>Korkut.Agent.Host.Controllers.LegalDocumentsController</c>)
/// plus search, for the standalone Karar Arama page. Public-database content only — no tenant data
/// — gated by documents.read like the <c>search_decisions</c>/<c>fetch_document</c> tools it mirrors.
/// </summary>
[ApiController]
[Route("api/legal")]
public sealed class LegalController(ILawClientsSearchService search, ILawClientsDocumentService documents) : ControllerBase
{
    [HttpGet("documents")]
    [Authorize(Policy = Permissions.ResearchUse)]
    public async Task<IActionResult> GetDocument(
        [FromQuery] string? source, [FromQuery] string? id, [FromQuery] string? articleId,
        [FromQuery] int? page, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(id))
        {
            throw new BadRequestException("'source' ve 'id' gereklidir.");
        }

        return Ok(await documents.GetAsync(source.Trim(), id, articleId: articleId, page: page, ct: ct));
    }

    [HttpGet("madde-tree")]
    [Authorize(Policy = Permissions.ResearchUse)]
    public async Task<IActionResult> GetMaddeTree([FromQuery] string? mevzuatId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mevzuatId))
        {
            throw new BadRequestException("'mevzuatId' gereklidir.");
        }

        return Ok(await documents.GetMaddeTreeAsync(mevzuatId.Trim(), ct));
    }

    [HttpGet("search")]
    [Authorize(Policy = Permissions.ResearchUse)]
    public async Task<IActionResult> Search(
        [FromQuery] string? source,
        [FromQuery] string? query,
        [FromQuery] int? limit,
        [FromQuery] int? page,
        [FromQuery] string? sort,
        [FromQuery] string? dateStart,
        [FromQuery] string? dateEnd,
        [FromQuery] string? chamber,
        [FromQuery] string? decisionType,
        [FromQuery] string? applicationType,
        [FromQuery] string? scope,
        [FromQuery] string? legislationType,
        [FromQuery] string? searchScope,
        [FromQuery] bool? tamCumle,
        [FromQuery] string? mevzuatNo,
        [FromQuery] string? gazetteNumber,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(query))
        {
            throw new BadRequestException("'source' ve 'query' gereklidir.");
        }

        var request = new LawClientsSearchRequest(
            query.Trim(), limit, page, sort, dateStart, dateEnd, chamber, decisionType, applicationType,
            scope, legislationType, searchScope, tamCumle, mevzuatNo, gazetteNumber);
        return Ok(await search.SearchAsync(source.Trim(), request, ct));
    }
}
