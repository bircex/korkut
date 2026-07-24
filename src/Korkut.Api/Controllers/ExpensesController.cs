using Korkut.Application.Expenses;
using Korkut.Application.Expenses.Requests;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>Expenses (masraflar). Forwards to <see cref="IExpenseService"/>.</summary>
[ApiController]
[Route("api/finance/expenses")]
public sealed class ExpensesController(IExpenseService expenses) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.BillingRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await expenses.ListAsync(ct));

    [HttpPost]
    [Authorize(Policy = Permissions.BillingWrite)]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest body, CancellationToken ct)
    {
        var created = await expenses.CreateAsync(body, ct);
        return Created($"/api/finance/expenses/{created.Id}", created);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.BillingWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await expenses.DeleteAsync(id, ct);
        return NoContent();
    }
}
