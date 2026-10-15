using Korkut.Contract;
using Korkut.Contract.Finance;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Finance.Controllers;

/// <summary>
/// Finance (faturalar, masraflar, para özeti). One controller over <see cref="IFinanceService"/>;
/// routes unchanged (/api/finance/invoices, /expenses, /summary). Maps Result to HTTP.
/// </summary>
[ApiController]
[Route("api/finance")]
public sealed class FinanceController(IFinanceService finance) : ControllerBase
{
    // ---- Invoices ----
    [HttpGet("invoices")]
    [Authorize(Policy = Permissions.FinanceRead)]
    public async Task<IActionResult> ListInvoices(CancellationToken ct)
        => (await finance.ListInvoicesAsync(ct)).ToListActionResult();

    [HttpPost("invoices")]
    [Authorize(Policy = Permissions.FinanceWrite)]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest body, CancellationToken ct)
        => (await finance.CreateInvoiceAsync(body, ct)).ToActionResult(i => Created($"/api/finance/invoices/{i.Id}", i));

    [HttpPatch("invoices/{id:guid}")]
    [Authorize(Policy = Permissions.FinanceWrite)]
    public async Task<IActionResult> UpdateInvoice(Guid id, [FromBody] UpdateInvoiceRequest body, CancellationToken ct)
        => (await finance.UpdateInvoiceAsync(id, body, ct)).ToActionResult();

    [HttpDelete("invoices/{id:guid}")]
    [Authorize(Policy = Permissions.FinanceWrite)]
    public async Task<IActionResult> DeleteInvoice(Guid id, CancellationToken ct)
        => (await finance.DeleteInvoiceAsync(id, ct)).ToActionResult();

    // ---- Expenses ----
    [HttpGet("expenses")]
    [Authorize(Policy = Permissions.FinanceRead)]
    public async Task<IActionResult> ListExpenses(CancellationToken ct)
        => (await finance.ListExpensesAsync(ct)).ToListActionResult();

    [HttpPost("expenses")]
    [Authorize(Policy = Permissions.FinanceWrite)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest body, CancellationToken ct)
        => (await finance.CreateExpenseAsync(body, ct)).ToActionResult(e => Created($"/api/finance/expenses/{e.Id}", e));

    [HttpDelete("expenses/{id:guid}")]
    [Authorize(Policy = Permissions.FinanceWrite)]
    public async Task<IActionResult> DeleteExpense(Guid id, CancellationToken ct)
        => (await finance.DeleteExpenseAsync(id, ct)).ToActionResult();

    // ---- Roll-up ----
    [HttpGet("summary")]
    [Authorize(Policy = Permissions.FinanceRead)]
    public async Task<IActionResult> Summary(CancellationToken ct)
        => (await finance.GetSummaryAsync(ct)).ToActionResult();
}
