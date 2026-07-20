using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Finance;
using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>EF/Postgres adapter for <see cref="IInvoiceRepository"/>; maps the persistence entity ↔ domain model.</summary>
public sealed class InvoiceRepository(KorkutDbContext db) : IInvoiceRepository
{
    public async Task<Invoice> AddAsync(Invoice invoice, CancellationToken ct = default)
    {
        db.Invoices.Add(ToEntity(invoice));
        await db.SaveChangesAsync(ct);
        return invoice;
    }

    public async Task<IReadOnlyList<Invoice>> ListAsync(Guid officeId, CancellationToken ct = default)
        => (await db.Invoices.AsNoTracking()
                .Where(x => x.OfficeId == officeId)
                .OrderByDescending(x => x.IssuedAt ?? x.CreatedAt)
                .ToListAsync(ct))
            .Select(ToDomain).ToList();

    public async Task<Invoice?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
    {
        var e = await db.Invoices.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id, ct);
        return e is null ? null : ToDomain(e);
    }

    public async Task<bool> UpdateAsync(Guid officeId, Guid id, string? status, decimal? amount, CancellationToken ct = default)
        => await db.Invoices.Where(x => x.OfficeId == officeId && x.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, x => status ?? x.Status)
                .SetProperty(x => x.Amount, x => amount ?? x.Amount), ct) > 0;

    public async Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.Invoices.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    private static Invoice ToDomain(InvoiceEntity e) =>
        new(e.Id, e.OfficeId, e.ClientId, e.MatterId, e.Amount, e.VatRate, e.Description, e.Status, e.IssuedAt, e.CreatedAt);

    private static InvoiceEntity ToEntity(Invoice i) => new()
    {
        Id = i.Id, OfficeId = i.OfficeId, ClientId = i.ClientId, MatterId = i.MatterId,
        Amount = i.Amount, VatRate = i.VatRate, Description = i.Description, Status = i.Status,
        IssuedAt = i.IssuedAt, CreatedAt = i.CreatedAt,
    };
}

/// <summary>EF/Postgres adapter for <see cref="IExpenseRepository"/>.</summary>
public sealed class ExpenseRepository(KorkutDbContext db) : IExpenseRepository
{
    public async Task<Expense> AddAsync(Expense expense, CancellationToken ct = default)
    {
        db.Expenses.Add(ToEntity(expense));
        await db.SaveChangesAsync(ct);
        return expense;
    }

    public async Task<IReadOnlyList<Expense>> ListAsync(Guid officeId, CancellationToken ct = default)
        => (await db.Expenses.AsNoTracking()
                .Where(x => x.OfficeId == officeId)
                .OrderByDescending(x => x.SpentAt ?? x.CreatedAt)
                .ToListAsync(ct))
            .Select(ToDomain).ToList();

    public async Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.Expenses.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    private static Expense ToDomain(ExpenseEntity e) =>
        new(e.Id, e.OfficeId, e.MatterId, e.ClientId, e.Amount, e.Kind, e.Description, e.SpentAt, e.CreatedAt);

    private static ExpenseEntity ToEntity(Expense x) => new()
    {
        Id = x.Id, OfficeId = x.OfficeId, MatterId = x.MatterId, ClientId = x.ClientId,
        Amount = x.Amount, Kind = x.Kind, Description = x.Description, SpentAt = x.SpentAt, CreatedAt = x.CreatedAt,
    };
}
