using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>Aggregate money view of the office: invoiced/paid/outstanding + total expenses.</summary>
public readonly record struct FinanceTotals(decimal Invoiced, decimal Paid, decimal Outstanding, decimal Expenses);

/// <summary>
/// Finance CRUD (invoices, expenses) + the office money summary. Tenant context is set
/// by the caller (RLS). Money is exact <see cref="decimal"/>; the invoice gross is
/// derived, never stored (see <see cref="Gross"/>).
/// </summary>
public sealed class FinanceStore(KorkutDbContext db, IClock clock)
{
    /// <summary>VAT-inclusive total for an invoice, rounded to 2 dp (banker's rounding).</summary>
    public static decimal Gross(decimal amount, decimal vatRate)
        => Math.Round(amount * (1m + vatRate), 2, MidpointRounding.ToEven);

    // --- Invoices ---

    public async Task<Guid> CreateInvoiceAsync(
        Guid officeId, decimal amount, decimal vatRate, Guid? clientId, Guid? matterId,
        string? description, string status, DateTimeOffset? issuedAt, CancellationToken ct = default)
    {
        var invoice = new InvoiceEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Amount = amount,
            VatRate = vatRate,
            ClientId = clientId,
            MatterId = matterId,
            Description = description,
            Status = status,
            IssuedAt = issuedAt ?? clock.UtcNow,
            CreatedAt = clock.UtcNow,
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        return invoice.Id;
    }

    public Task<List<InvoiceEntity>> ListInvoicesAsync(Guid officeId, CancellationToken ct = default)
        => db.Invoices.AsNoTracking()
            .Where(x => x.OfficeId == officeId)
            .OrderByDescending(x => x.IssuedAt ?? x.CreatedAt)
            .ToListAsync(ct);

    public Task<InvoiceEntity?> GetInvoiceAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => db.Invoices.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id, ct);

    /// <summary>Patch an invoice's status and/or net amount. Returns false if missing.</summary>
    public async Task<bool> UpdateInvoiceAsync(
        Guid officeId, Guid id, string? status, decimal? amount, CancellationToken ct = default)
    {
        var q = db.Invoices.Where(x => x.OfficeId == officeId && x.Id == id);
        var affected = await q.ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, x => status ?? x.Status)
            .SetProperty(x => x.Amount, x => amount ?? x.Amount), ct);
        return affected > 0;
    }

    public async Task<bool> DeleteInvoiceAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.Invoices.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    // --- Expenses ---

    public async Task<Guid> CreateExpenseAsync(
        Guid officeId, decimal amount, Guid? matterId, Guid? clientId,
        string? kind, string? description, DateTimeOffset? spentAt, CancellationToken ct = default)
    {
        var expense = new ExpenseEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Amount = amount,
            MatterId = matterId,
            ClientId = clientId,
            Kind = kind,
            Description = description,
            SpentAt = spentAt ?? clock.UtcNow,
            CreatedAt = clock.UtcNow,
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return expense.Id;
    }

    public Task<List<ExpenseEntity>> ListExpensesAsync(Guid officeId, CancellationToken ct = default)
        => db.Expenses.AsNoTracking()
            .Where(x => x.OfficeId == officeId)
            .OrderByDescending(x => x.SpentAt ?? x.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> DeleteExpenseAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.Expenses.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    // --- Summary ---

    /// <summary>
    /// Office money totals. Invoiced/paid are GROSS (VAT-inclusive) and exclude cancelled
    /// invoices; outstanding = invoiced − paid. Computed in-process from the (tenant-scoped)
    /// rows so the derived gross stays consistent with the read model.
    /// </summary>
    public async Task<FinanceTotals> SummaryAsync(Guid officeId, CancellationToken ct = default)
    {
        var invoices = await db.Invoices.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.Status != "cancelled")
            .Select(x => new { x.Amount, x.VatRate, x.Status })
            .ToListAsync(ct);

        var invoiced = invoices.Sum(i => Gross(i.Amount, i.VatRate));
        var paid = invoices.Where(i => i.Status == "paid").Sum(i => Gross(i.Amount, i.VatRate));
        var expenses = await db.Expenses.AsNoTracking()
            .Where(x => x.OfficeId == officeId)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

        return new FinanceTotals(invoiced, paid, invoiced - paid, expenses);
    }
}
