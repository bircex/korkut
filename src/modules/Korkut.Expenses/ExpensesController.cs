using Korkut.Contract.Expenses;
using Korkut.Core;
using Korkut.Web.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Expenses;

/// <summary>Expenses (masraflar). Forwards to <see cref="IExpenseService"/>, maps Result to HTTP.</summary>
[ApiController]
[Route("api/finance/expenses")]
public sealed class ExpensesController(IExpenseService expenses) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.BillingRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await expenses.ListAsync(ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.BillingWrite)]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest body, CancellationToken ct)
        => (await expenses.CreateAsync(body, ct)).ToActionResult(e => Created($"/api/finance/expenses/{e.Id}", e));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.BillingWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await expenses.DeleteAsync(id, ct)).ToActionResult();
}
