using Korkut.Application.Clients;
using Korkut.Application.Clients.Requests;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Clients;

/// <summary>
/// Clients (müvekkiller). A pure transport: each action forwards to <see cref="IClientService"/>,
/// which owns the tenant scope, the use-case and the DTO mapping. Paths match the frozen contract.
/// </summary>
[ApiController]
[Route("api/clients")]
public sealed class ClientsController(IClientService clients) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.ClientsRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await clients.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.ClientsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await clients.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.ClientsWrite)]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest body, CancellationToken ct)
    {
        var created = await clients.CreateAsync(body, ct);
        return Created($"/api/clients/{created.Id}", created);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Permissions.ClientsWrite)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest body, CancellationToken ct)
        => Ok(await clients.UpdateAsync(id, body, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.ClientsWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await clients.DeleteAsync(id, ct);
        return NoContent();
    }
}
