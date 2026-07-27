using Korkut.Contract.Matters;
using Korkut.Core;
using Korkut.Web.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Matters.Controllers;

/// <summary>
/// Matters (dosyalar). A pure transport: each action forwards to <see cref="IMatterService"/> and
/// maps the returned Result to HTTP.
/// </summary>
[ApiController]
[Route("api/matters")]
public sealed class MattersController(IMatterService matters) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await matters.ListAsync(ct)).ToActionResult();

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => (await matters.GetAsync(id, ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromBody] CreateMatterRequest body, CancellationToken ct)
        => (await matters.CreateAsync(body, ct)).ToActionResult(m => Created($"/api/matters/{m.Id}", m));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.MattersDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await matters.DeleteAsync(id, ct)).ToActionResult();
}
