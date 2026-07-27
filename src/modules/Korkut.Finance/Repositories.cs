using Korkut.Core;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Finance;

/// <summary>Persistence port for invoices; listings are most-recently-issued first.</summary>
public interface IInvoiceRepository : IRepository<Invoice>;

/// <summary>Persistence port for expenses; listings are most-recently-spent first.</summary>
public interface IExpenseRepository : IRepository<Expense>;

/// <summary>EF/Postgres adapter for <see cref="IInvoiceRepository"/> over the ambient DbContext.</summary>
public sealed class InvoiceRepository(DbContext db) : Repository<Invoice>(db), IInvoiceRepository
{
    public override async Task<IReadOnlyList<Invoice>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderByDescending(x => x.IssuedAt ?? x.CreatedAt).ToListAsync(ct);
}

/// <summary>EF/Postgres adapter for <see cref="IExpenseRepository"/> over the ambient DbContext.</summary>
public sealed class ExpenseRepository(DbContext db) : Repository<Expense>(db), IExpenseRepository
{
    public override async Task<IReadOnlyList<Expense>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderByDescending(x => x.SpentAt ?? x.CreatedAt).ToListAsync(ct);
}
