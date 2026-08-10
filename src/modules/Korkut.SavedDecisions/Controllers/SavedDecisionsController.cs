using Korkut.Contract;
using Korkut.Contract.SavedDecisions;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.SavedDecisions.Controllers;

/// <summary>
/// Kararlarım: the office's bookmarked decisions from Karar Arama / İlgili Kararlar. Same
/// documents.read/write policy as the search it bookmarks from.
/// </summary>
[ApiController]
[Route("api/saved-decisions")]
public sealed class SavedDecisionsController(ISavedDecisionService decisions) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.DocumentsRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await decisions.ListAsync(ct)).ToListActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.DocumentsWrite)]
    public async Task<IActionResult> Save([FromBody] SaveDecisionRequest body, CancellationToken ct)
        => (await decisions.SaveAsync(body, ct)).ToActionResult(d => Created($"/api/saved-decisions/{d.Id}", d));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.DocumentsWrite)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
        => (await decisions.RemoveAsync(id, ct)).ToActionResult();
}
