using Korkut.Contract.Clients;
using Korkut.Core;
using Korkut.Web.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Clients;

/// <summary>
/// Clients (müvekkiller). A pure transport: each action forwards to <see cref="IClientService"/>
/// and maps the returned <c>Result</c> to HTTP via <see cref="ApiResults"/>. Paths match the
/// frozen contract.
/// </summary>
[ApiController]
[Route("api/clients")]
public sealed class ClientsController(IClientService clients) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.ClientsRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await clients.ListAsync(ct)).ToActionResult();

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.ClientsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => (await clients.GetAsync(id, ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.ClientsWrite)]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest body, CancellationToken ct)
        => (await clients.CreateAsync(body, ct)).ToActionResult(c => Created($"/api/clients/{c.Id}", c));

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Permissions.ClientsWrite)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest body, CancellationToken ct)
        => (await clients.UpdateAsync(id, body, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.ClientsWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await clients.DeleteAsync(id, ct)).ToActionResult();
}
