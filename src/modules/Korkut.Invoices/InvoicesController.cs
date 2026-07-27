using Korkut.Contract.Invoices;
using Korkut.Core;
using Korkut.Web.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Invoices;

/// <summary>Invoices (faturalar). Forwards to <see cref="IInvoiceService"/>, maps Result to HTTP.</summary>
[ApiController]
[Route("api/finance/invoices")]
public sealed class InvoicesController(IInvoiceService invoices) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.BillingRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await invoices.ListAsync(ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.BillingWrite)]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest body, CancellationToken ct)
        => (await invoices.CreateAsync(body, ct)).ToActionResult(i => Created($"/api/finance/invoices/{i.Id}", i));

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Permissions.BillingWrite)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInvoiceRequest body, CancellationToken ct)
        => (await invoices.UpdateAsync(id, body, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.BillingWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await invoices.DeleteAsync(id, ct)).ToActionResult();
}
