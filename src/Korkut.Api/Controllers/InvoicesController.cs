using Korkut.Core;
using Korkut.Application.Invoices;
using Korkut.Application.Invoices.Requests;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>Invoices (faturalar). Forwards to <see cref="IInvoiceService"/>.</summary>
[ApiController]
[Route("api/finance/invoices")]
public sealed class InvoicesController(IInvoiceService invoices) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.BillingRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await invoices.ListAsync(ct));

    [HttpPost]
    [Authorize(Policy = Permissions.BillingWrite)]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest body, CancellationToken ct)
    {
        var created = await invoices.CreateAsync(body, ct);
        return Created($"/api/finance/invoices/{created.Id}", created);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Permissions.BillingWrite)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInvoiceRequest body, CancellationToken ct)
        => Ok(await invoices.UpdateAsync(id, body, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.BillingWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await invoices.DeleteAsync(id, ct);
        return NoContent();
    }
}
