using Korkut.Application.Matters;
using Korkut.Application.Matters.Requests;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>
/// Matters (dosyalar). A pure transport: each action forwards to <see cref="IMatterService"/>,
/// which owns the tenant scope, the use-case and the DTO mapping.
/// </summary>
[ApiController]
[Route("api/matters")]
public sealed class MattersController(IMatterService matters) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await matters.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await matters.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromBody] CreateMatterRequest body, CancellationToken ct)
    {
        var created = await matters.CreateAsync(body, ct);
        return Created($"/api/matters/{created.Id}", created);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.MattersDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await matters.DeleteAsync(id, ct);
        return NoContent();
    }
}
